using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Reports;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>حضور الطلاب لفترة (6.4 — ق-1): «من بعد إلى» يُرفض قبل القراءة · التجميع (طالب × فوج) والعدّ بالحالة · المبرر يُحسب ولا يدخل النسبة (D-93) · الأكثر غياباً أولاً · الإلغاء يُرمى (D-64) · غير المتوقع عربي نظيف (D-24)</summary>
public sealed class GetAttendanceSummaryHandlerTests
{
    /// <summary>مزيّف التقارير لق-1 — قراءة علامات الحضور وحدها تعمل، والباقي لا يُستدعى في المختبَر</summary>
    private sealed class ReportRepoFake : IReportRepository
    {
        public IReadOnlyList<AttendanceMarkRaw> MarksToReturn { get; set; } = new List<AttendanceMarkRaw>();
        public Exception? ToThrow { get; set; }
        public bool Called { get; private set; }

        public Task<IReadOnlyList<AttendanceMarkRaw>> GetAttendanceMarksForPeriodAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default)
        {
            Called = true;
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(MarksToReturn);
        }

        public Task<StudentPaymentsRead> GetPaymentsWithAllocationsForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReceiptPrintRead?> GetReceiptForPrintAsync(int paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<EnrollmentBalanceRaw>> GetActiveEnrollmentBalancesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static AttendanceMarkRaw Mark(int studentId, string name, AttendanceStatus status, int groupId = 10, string groupName = "فيزياء أ")
        => new(studentId, name, groupId, groupName, status);

    private static (GetAttendanceSummaryHandler handler, ReportRepoFake reports) Build(
        IReadOnlyList<AttendanceMarkRaw>? marks = null, Exception? toThrow = null)
    {
        var reports = new ReportRepoFake { MarksToReturn = marks ?? new List<AttendanceMarkRaw>(), ToThrow = toThrow };
        return (new GetAttendanceSummaryHandler(reports, NullLogger<GetAttendanceSummaryHandler>.Instance), reports);
    }

    [Fact]
    public async Task FromAfterTo_ValidationFailure_RepositoryNotCalled()
    {
        var (handler, reports) = Build();

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 1), null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.False(reports.Called);   // التحقق قبل أي قراءة
    }

    [Fact]
    public async Task Success_GroupsByStudentAndGroup_CountsAndPercent()   // المبرر يُحسب ولا يدخل النسبة (D-93)
    {
        var (handler, _) = Build(new List<AttendanceMarkRaw>
        {
            Mark(1, "أمين", AttendanceStatus.Present),
            Mark(1, "أمين", AttendanceStatus.Present),
            Mark(1, "أمين", AttendanceStatus.Present),
            Mark(1, "أمين", AttendanceStatus.Absent),
            Mark(1, "أمين", AttendanceStatus.Justified),
            Mark(2, "سارا", AttendanceStatus.Present),
            Mark(2, "سارا", AttendanceStatus.Present),
        });

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(new DateOnly(2026, 8, 1), report.From);
        Assert.Equal(new DateOnly(2026, 8, 27), report.To);

        Assert.Equal(2, report.Rows.Count);
        var first = report.Rows[0];   // الأكثر غياباً أولاً — هم سؤال المكتب
        Assert.Equal("أمين", first.StudentName);
        Assert.Equal(3, first.PresentCount);
        Assert.Equal(1, first.AbsentCount);
        Assert.Equal(1, first.JustifiedCount);
        Assert.Equal(5, first.MarkedCount);
        Assert.Equal("75%", first.AttendancePercentText);   // 3 من (3+1) — المبرر خارج المقسوم

        var second = report.Rows[1];
        Assert.Equal("سارا", second.StudentName);
        Assert.Equal(2, second.PresentCount);
        Assert.Equal(0, second.AbsentCount);
        Assert.Equal("100%", second.AttendancePercentText);

        Assert.Equal(5, report.PresentTotal);
        Assert.Equal(1, report.AbsentTotal);
        Assert.Equal(1, report.JustifiedTotal);
        Assert.Equal("83%", report.OverallPercentText);   // 5 من 6
    }

    [Fact]
    public async Task SameStudent_TwoGroups_TwoRows()   // التجميع (طالب × فوج) لا طالباً فقط
    {
        var (handler, _) = Build(new List<AttendanceMarkRaw>
        {
            Mark(1, "أمين", AttendanceStatus.Present, groupId: 10, groupName: "فيزياء أ"),
            Mark(1, "أمين", AttendanceStatus.Absent, groupId: 20, groupName: "رياضيات ب"),
        });

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Rows.Count);
    }

    [Fact]
    public async Task EmptyPeriod_EmptyRows_DashPercent()
    {
        var (handler, _) = Build();

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Rows);
        Assert.Equal("—", result.Value.OverallPercentText);
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64
    {
        var (handler, _) = Build(toThrow: new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null));
    }

    [Fact]
    public async Task UnexpectedException_ArabicFailure()   // D-24
    {
        var (handler, _) = Build(toThrow: new InvalidOperationException("raw boom"));

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);
    }
}
