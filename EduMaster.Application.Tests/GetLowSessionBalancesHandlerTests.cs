using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Reports;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>تنبيه نفاد أرصدة الحصص (6.4 — ق-5): العتبة الافتراضية 2 (عرف الشهر 4 حصص — D-91) · الدلالة «الرصيد ≤ العتبة» (على العتبة يُدرَج — الصفر نفاد تام) · السالب مدرَج (D-92) والأنفد أولاً · عتبة سالبة تُرفض قبل القراءة · جهة التذكير: الولي ثم الطالب (D-36) · الإلغاء يُرمى (D-64) · غير المتوقع عربي نظيف (D-24)</summary>
public sealed class GetLowSessionBalancesHandlerTests
{
    /// <summary>مزيّف التقارير لق-5 — قراءة الأرصدة وحدها تعمل، والباقي لا يُستدعى في المختبَر</summary>
    private sealed class ReportRepoFake : IReportRepository
    {
        public IReadOnlyList<EnrollmentBalanceRaw> BalancesToReturn { get; set; } = new List<EnrollmentBalanceRaw>();
        public Exception? ToThrow { get; set; }
        public bool Called { get; private set; }

        public Task<IReadOnlyList<EnrollmentBalanceRaw>> GetActiveEnrollmentBalancesAsync(CancellationToken cancellationToken = default)
        {
            Called = true;
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(BalancesToReturn);
        }

        public Task<StudentPaymentsRead> GetPaymentsWithAllocationsForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReceiptPrintRead?> GetReceiptForPrintAsync(int paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AttendanceMarkRaw>> GetAttendanceMarksForPeriodAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static EnrollmentBalanceRaw Balance(int enrollmentId, string name, int purchased, int consumed)
        => new(enrollmentId, enrollmentId * 10, name, 10, "فيزياء أ", "الفيزياء", purchased, consumed, "الولي", "0550001122", null);

    private static (GetLowSessionBalancesHandler handler, ReportRepoFake reports) Build(
        IReadOnlyList<EnrollmentBalanceRaw>? balances = null, Exception? toThrow = null)
    {
        var reports = new ReportRepoFake { BalancesToReturn = balances ?? new List<EnrollmentBalanceRaw>(), ToThrow = toThrow };
        return (new GetLowSessionBalancesHandler(reports, NullLogger<GetLowSessionBalancesHandler>.Instance), reports);
    }

    [Fact]
    public async Task DefaultThreshold_FiltersAboveTwo_AndOrdersExhaustedFirst()
    {
        var (handler, reports) = Build(new List<EnrollmentBalanceRaw>
        {
            Balance(1, "أمين", purchased: 5, consumed: 4),    // رصيد 1
            Balance(2, "سارا", purchased: 4, consumed: 5),    // رصيد −1 (تجاوز مسموح — D-92)
            Balance(3, "ياسين", purchased: 6, consumed: 3),   // رصيد 3 — فوق العتبة الافتراضية فيُستبعد
            Balance(4, "لينا", purchased: 2, consumed: 0),    // رصيد 2 — على العتبة فيُدرج
        });

        var result = await handler.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.True(reports.Called);
        var items = result.Value!;
        Assert.Equal(3, items.Count);
        Assert.Equal(new[] { -1, 1, 2 }, items.Select(i => i.Balance).ToArray());   // الأنفد أولاً
        Assert.Equal("سارا", items[0].StudentName);
        Assert.True(items[0].IsNegative);
        Assert.Equal("الولي", items[0].ContactName);          // جهة التذكير: الولي أولاً (D-36)
        Assert.Equal("0550001122", items[0].ContactPhone);
    }

    [Fact]
    public async Task CustomThreshold_Zero_IncludesZeroAndNegative()   // الدلالة «≤ العتبة» — الصفر (نفاد تام) يُدرَج مثلما يُدرَج 2 عند الافتراضية
    {
        var (handler, _) = Build(new List<EnrollmentBalanceRaw>
        {
            Balance(1, "أمين", purchased: 4, consumed: 4),    // 0 — على العتبة فيُدرَج
            Balance(2, "سارا", purchased: 4, consumed: 5),    // −1
            Balance(3, "ياسين", purchased: 5, consumed: 4),   // 1 — فوقها فيُستبعد
        });

        var result = await handler.ExecuteAsync(threshold: 0);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { -1, 0 }, result.Value!.Select(i => i.Balance).ToArray());   // الأنفد أولاً
    }

    [Fact]
    public async Task NegativeThreshold_ValidationFailure_RepositoryNotCalled()
    {
        var (handler, reports) = Build();

        var result = await handler.ExecuteAsync(threshold: -1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.False(reports.Called);   // التحقق قبل أي قراءة
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64
    {
        var (handler, _) = Build(toThrow: new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.ExecuteAsync());
    }

    [Fact]
    public async Task UnexpectedException_ArabicFailure()   // D-24
    {
        var (handler, _) = Build(toThrow: new InvalidOperationException("raw boom"));

        var result = await handler.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);
    }
}
