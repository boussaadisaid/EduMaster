using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>عكس إيصال القبض الخاطئ (6.6-ع-4): التركيب الكامل (صرف معاكس موسوم + فكّ تخصيصات الأصل — إزالة مصمَّمة ع-ب2) في معاملة · الحُراس: قبض فقط · لا عكس مرتين · سبب إلزامي · غير المتوقع عربي بتراجع</summary>
public class ReverseReceiptHandlerTests
{
    private static ReceiptReversalInfoRaw ReceiptInfo(bool alreadyReversed = false, byte kind = 1) =>
        new(StudentId: 9, TreasuryAccountId: 1, Kind: kind, AmountCentimes: 140000, ReceiptNo: 41, AlreadyReversed: alreadyReversed);

    private static (ReverseReceiptHandler handler, FakePaymentRepository payments, FakeUnitOfWork uow) Build(
        ReceiptReversalInfoRaw? info)
    {
        var payments = new FakePaymentRepository { NextReceiptNo = 77, ReversalInfoToReturn = info };
        var uow = new FakeUnitOfWork();
        return (new ReverseReceiptHandler(payments, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<ReverseReceiptHandler>.Instance), payments, uow);
    }

    [Fact]
    public async Task Success_ComposesCounterRefund_AndNegativeReversals_OnOriginal()
    {
        var (handler, payments, uow) = Build(ReceiptInfo());

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(55, "تصحيح خطأ إدخال"));

        Assert.True(result.IsSuccess);
        Assert.Equal(77, result.Value);   // رقم إيصال العكس من السلسلة نفسها (D-105)
        var reversal = Assert.Single(payments.Payments);
        Assert.Equal(PaymentKind.Refund, reversal.Kind);
        Assert.Equal(140000, reversal.AmountCentimes);
        Assert.Equal(9, reversal.StudentId);
        Assert.Equal("↩ عكس الإيصال #000041 — تصحيح خطأ إدخال", reversal.Note);
        Assert.Equal(55, Assert.Single(payments.DeletedAllocationsForPayments));   // فُكّت تخصيصات الإيصال 55 في المعاملة
        Assert.Empty(payments.Allocations);                                        // لا سطور جديدة — فكّ في المكان (الزوج فريد ومشروط الموجب)
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task ReceiptWithoutAllocations_Reverses_CashOnly()
    {
        var (handler, payments, _) = Build(ReceiptInfo());

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(55, "تصحيح خطأ إدخال"));

        Assert.True(result.IsSuccess);
        Assert.Single(payments.Payments);        // الصرف المعاكس فقط
        Assert.Empty(payments.Allocations);      // بلا تخصيصات تُفكّ
    }

    [Fact]
    public async Task Refund_IsNotReversible()
    {
        var (handler, _, uow) = Build(ReceiptInfo(kind: 2));

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(55, "عكس صرف"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Equal("العكس لإيصالات القبض فقط — الاسترجاع يُعالج بقبض جديد مصحّح.", result.ErrorMessage);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task AlreadyReversed_Conflict_NoDoubleReversal()
    {
        var (handler, _, uow) = Build(ReceiptInfo(alreadyReversed: true));

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(55, "تصحيح"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("هذا الإيصال عُكس من قبل (#000041) — لا يُعكَس مرتين.", result.ErrorMessage);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task MissingReceipt_NotFound_WithoutTransaction()
    {
        var (handler, _, uow) = Build(null);

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(99, "تصحيح"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task BlankReason_Validation()
    {
        var (handler, _, _) = Build(ReceiptInfo());

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(55, " "));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("سبب العكس إلزامي — الإيصال المعكوس يُوثَّق دائماً.", result.ErrorMessage);
    }

    [Fact]
    public async Task Unexpected_FailsArabicGeneric_WithRollback()
    {
        // فشل قراءة بطاقة العكس — المزيّف يرمي
        var payments = new FakePaymentRepository { ToThrowOnReversalRead = new InvalidOperationException("boom") };
        var uow = new FakeUnitOfWork();
        var handler = new ReverseReceiptHandler(payments, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<ReverseReceiptHandler>.Instance);

        var result = await handler.ExecuteAsync(new ReverseReceiptRequest(55, "تصحيح"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.Equal("حدث خطأ غير متوقع أثناء عكس الإيصال.", result.ErrorMessage);
        Assert.Equal(1, uow.RolledBackCount);
    }
}
