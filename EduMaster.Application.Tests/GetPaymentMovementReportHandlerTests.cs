using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Reports;
using EduMaster.Domain.Billing;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>حركة القبض لفترة (6.1 — D-127/ت-6): «من بعد إلى» يُرفض قبل أي قراءة · السجل يُغلَّف بإجماليات مشتقة · الإلغاء يُرمى (D-64) · غير المتوقع عربي نظيف (D-24)</summary>
public sealed class GetPaymentMovementReportHandlerTests
{
    /// <summary>مزيّف المدفوعات لقراءة التقرير — GetForPeriodAsync وحدها تعمل، والباقي لا يُستدعى في المختبَر</summary>
    private sealed class PaymentRepoFake : IPaymentRepository
    {
        public IReadOnlyList<PaymentListItem> LogToReturn { get; set; } = new List<PaymentListItem>();
        public Exception? ToThrow { get; set; }
        public bool GetForPeriodCalled { get; private set; }

        public Task<IEnumerable<PaymentListItem>> GetForPeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            GetForPeriodCalled = true;
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(LogToReturn.AsEnumerable());
        }

        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAllocationAsync(PaymentAllocation allocation, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<long> GetUnallocatedForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<UnallocatedReceiptRaw>> GetUnallocatedReceiptsForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReceiptReversalInfoRaw?> GetReceiptReversalInfoAsync(int paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAllocationsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAllocationsForChargeAsync(int chargeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static PaymentListItem LogRow(int id, PaymentKind kind, long amount, long allocated = 0) =>
        new(id, 100 + id, kind, $"طالب {id}", null, amount, new DateTime(2026, 8, 25), null, allocated);

    private static (GetPaymentMovementReportHandler handler, PaymentRepoFake payments) Build(
        IReadOnlyList<PaymentListItem>? log = null, Exception? toThrow = null)
    {
        var payments = new PaymentRepoFake { LogToReturn = log ?? new List<PaymentListItem>(), ToThrow = toThrow };
        return (new GetPaymentMovementReportHandler(payments, NullLogger<GetPaymentMovementReportHandler>.Instance), payments);
    }

    [Fact]
    public async Task FromAfterTo_ValidationFailure_RepositoryNotCalled()
    {
        var (handler, payments) = Build();

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 25), new DateOnly(2026, 8, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.False(payments.GetForPeriodCalled);   // التحقق قبل أي قراءة
    }

    [Fact]
    public async Task Success_WrapsLogRows_WithDerivedTotals()
    {
        var (handler, _) = Build(new List<PaymentListItem>
        {
            LogRow(1, PaymentKind.Receipt, 50000, allocated: 20000),
            LogRow(2, PaymentKind.Refund, 10000),
        });

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 25));

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(50000, report.ReceiptsTotalCentimes);
        Assert.Equal(10000, report.RefundsTotalCentimes);
        Assert.Equal(40000, report.NetCentimes);
        Assert.Equal(new DateOnly(2026, 8, 1), report.From);
        Assert.Equal(new DateOnly(2026, 8, 25), report.To);
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64: الإلغاء ليس خطأً
    {
        var (handler, _) = Build(toThrow: new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 25)));
    }

    [Fact]
    public async Task UnexpectedException_ArabicFailure()   // D-24: لا نص استثناء خام أبداً
    {
        var (handler, _) = Build(toThrow: new InvalidOperationException("raw boom"));

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 25));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);
    }
}