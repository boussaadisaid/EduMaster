using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>تسوية المستحقات (D-108): إلغاء وتخفيض موثقان — بلا حذف (D-109)</summary>
public sealed class ChargeSettlementHandlersTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);

    private static Domain.Billing.Charge BuildActiveCharge(long amountCentimes = 100000) =>
        Domain.Billing.Charge.Load(
            id: 5, studentId: 2, kind: ChargeKind.RegistrationFee, annualEnrollmentId: 7, groupSessionPurchaseId: null,
            originalAmountCentimes: amountCentimes, amountCentimes: amountCentimes,
            status: ChargeStatus.Active, adjustmentNote: null, cancelledAtUtc: null,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static (CancelChargeHandler handler, FakeChargeRepository charges, FakeUnitOfWork uow) BuildCancel(Domain.Billing.Charge? charge)
    {
        var full = BuildCancelFull(charge);
        return (full.handler, full.charges, full.uow);   // التوقيع القديم يبقى — المدفوعات لاختبارات العكس الجديدة (ع-ب)
    }

    private static (CancelChargeHandler handler, FakeChargeRepository charges, FakePaymentRepository payments, FakeUnitOfWork uow) BuildCancelFull(Domain.Billing.Charge? charge)
    {
        var charges = new FakeChargeRepository { EntityToReturn = charge };
        var payments = new FakePaymentRepository();
        var uow = new FakeUnitOfWork();
        var handler = new CancelChargeHandler(charges, payments, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<CancelChargeHandler>.Instance);
        return (handler, charges, payments, uow);
    }

    private static (ReduceChargeHandler handler, FakeChargeRepository charges, FakeUnitOfWork uow) BuildReduce(Domain.Billing.Charge? charge)
    {
        var charges = new FakeChargeRepository { EntityToReturn = charge };
        var uow = new FakeUnitOfWork();
        var handler = new ReduceChargeHandler(charges, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<ReduceChargeHandler>.Instance);
        return (handler, charges, uow);
    }

    // ---------- حارس المخصوص (6.6-ع-3): لا تخفيض تحته — والحدّ «يساويه» يمرّ ----------
    [Fact]
    public async Task Reduce_BelowAllocated_Rejected_BusinessRule()
    {
        var (handler, charges, uow) = BuildReduce(BuildActiveCharge(100000));
        charges.AllocatedForChargeValue = 60000;   // مخصوص 600.00 دج على مستحق 1000.00 دج

        var result = await handler.ExecuteAsync(new ReduceChargeRequest(5, 40000, "تخفيض تحت المخصوص"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Equal("لا يُخفَّض مستحق تحت مخصوصه (600.00 دج) — ألغِه أو خفّضه فوقها.", result.ErrorMessage);
        Assert.Empty(charges.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task Reduce_ToExactlyAllocated_Allowed_Boundary()
    {
        var (handler, charges, uow) = BuildReduce(BuildActiveCharge(100000));
        charges.AllocatedForChargeValue = 60000;

        var result = await handler.ExecuteAsync(new ReduceChargeRequest(5, 60000, "إلى المخصوص تماماً"));

        Assert.True(result.IsSuccess);
        Assert.Single(charges.Updated);
        Assert.Equal(1, uow.CommittedCount);
    }

    // ---------- تحرير مال الملغي (6.6-ع-ب2 / ع-1): فكّ تخصيصاته في المعاملة نفسها — الجدول فريد الزوج ومشروط الموجب فالإزالة مصمَّمة ----------
    [Fact]
    public async Task Cancel_DeletesItsAllocations_InSameTransaction_ReturningMoney()
    {
        var (handler, charges, payments, uow) = BuildCancelFull(BuildActiveCharge(100000));

        var result = await handler.ExecuteAsync(new CancelChargeRequest(5, "أُلغي — ماله يعود للزائدة"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChargeStatus.Cancelled, Assert.Single(charges.Updated).Status);
        Assert.Equal(5, Assert.Single(payments.DeletedAllocationsForCharges));   // فُكّت تخصيصات المستحق 5
        Assert.Empty(payments.Allocations);                                       // لا سطور جديدة — فكّ في المكان
        Assert.Equal(1, uow.CommittedCount);
    }

    // ---------- الإلغاء ----------
    [Fact]
    public async Task Cancel_ActiveCharge_SettlesAndCommits()
    {
        var (handler, charges, uow) = BuildCancel(BuildActiveCharge());

        var result = await handler.ExecuteAsync(new CancelChargeRequest(5, "خطأ إدخال"));

        Assert.True(result.IsSuccess);
        var updated = Assert.Single(charges.Updated);
        Assert.Equal(ChargeStatus.Cancelled, updated.Status);
        Assert.Equal("خطأ إدخال", updated.AdjustmentNote);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task Cancel_MissingCharge_NotFound_WithoutWriting()
    {
        var (handler, charges, uow) = BuildCancel(null);

        var result = await handler.ExecuteAsync(new CancelChargeRequest(99, "سبب"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(charges.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task Cancel_WithoutReason_ValidationError()
    {
        var (handler, charges, uow) = BuildCancel(BuildActiveCharge());

        var result = await handler.ExecuteAsync(new CancelChargeRequest(5, "  "));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(charges.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    // ---------- التخفيض ----------
    [Fact]
    public async Task Reduce_ToLowerAmount_SettlesAndCommits()
    {
        var (handler, charges, uow) = BuildReduce(BuildActiveCharge(100000));

        var result = await handler.ExecuteAsync(new ReduceChargeRequest(5, 60000, "تخفيض متفق عليه"));

        Assert.True(result.IsSuccess);
        var updated = Assert.Single(charges.Updated);
        Assert.Equal(60000, updated.AmountCentimes);
        Assert.Equal(100000, updated.OriginalAmountCentimes);   // الأصلي محفوظ
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task Reduce_NegativeAmount_ValidationError_BeforeAnyRead()
    {
        var (handler, charges, uow) = BuildReduce(BuildActiveCharge());

        var result = await handler.ExecuteAsync(new ReduceChargeRequest(5, -1, "سبب"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(charges.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task Reduce_ToSameAmount_ValidationError()   // ليس تخفيضاً
    {
        var (handler, charges, uow) = BuildReduce(BuildActiveCharge(100000));

        var result = await handler.ExecuteAsync(new ReduceChargeRequest(5, 100000, "سبب"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(charges.Updated);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task Reduce_MissingCharge_NotFound()
    {
        var (handler, _, _) = BuildReduce(null);

        var result = await handler.ExecuteAsync(new ReduceChargeRequest(99, 1000, "سبب"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }
}