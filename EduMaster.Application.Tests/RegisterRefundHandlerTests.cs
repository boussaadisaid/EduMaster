
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>إيصال الصرف (D-108 — ختام UC-30): من الزائدة الدائنة فقط · سبب إلزامي · لا تخصيص أبداً</summary>
public sealed class RegisterRefundHandlerTests
{
    private static (RegisterRefundHandler handler, FakePaymentRepository payments, FakeUnitOfWork uow) Build(long availableCredit)
    {
        var payments = new FakePaymentRepository { NextReceiptNo = 42, UnallocatedValue = availableCredit };
        var uow = new FakeUnitOfWork();
        var handler = new RegisterRefundHandler(payments, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<RegisterRefundHandler>.Instance);
        return (handler, payments, uow);
    }

    private static RegisterRefundRequest Refund(long amount, string reason = "استرجاع زائدة بعد التسوية") =>
        new(StudentId: 2, AmountCentimes: amount, PaidOn: new DateOnly(2026, 8, 23), Reason: reason);

    [Fact]
    public async Task WithinCredit_WritesRefundReceipt_InOneCommit_WithoutAllocations()
    {
        var (handler, payments, uow) = Build(availableCredit: 50000);

        var result = await handler.ExecuteAsync(Refund(30000));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);                                  // نفس سلسلة الإيصالات (D-105)
        var refund = Assert.Single(payments.Payments);
        Assert.Equal(PaymentKind.Refund, refund.Kind);
        Assert.Equal(2, refund.StudentId);
        Assert.Equal(30000, refund.AmountCentimes);
        Assert.Equal("استرجاع زائدة بعد التسوية", refund.Note);          // السبب في خانة الملاحظة
        Assert.Empty(payments.Allocations);                              // الصرف لا يُخصَّص أبداً
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task ExceedingCredit_BusinessRule_WithoutWriting()    // الحارس الأهم: لا صرف من الهواء
    {
        var (handler, payments, uow) = Build(availableCredit: 20000);

        var result = await handler.ExecuteAsync(Refund(30000));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Contains("الزائدة الدائنة", result.ErrorMessage);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task ZeroCredit_RejectsAnyRefund()
    {
        var (handler, payments, uow) = Build(availableCredit: 0);

        var result = await handler.ExecuteAsync(Refund(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task NonPositiveAmount_ValidationError_BeforeAnyRead()
    {
        var (handler, payments, uow) = Build(availableCredit: 50000);

        var result = await handler.ExecuteAsync(Refund(0));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task FuturePaidOn_ValidationError()
    {
        var (handler, payments, uow) = Build(availableCredit: 50000);

        var result = await handler.ExecuteAsync(
            new RegisterRefundRequest(2, 30000, new DateOnly(2026, 8, 30), "سبب"));   // اليوم المزيّف 2026-08-23

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task EmptyReason_ValidationError()                    // المال الخارج يُوثَّق دائماً
    {
        var (handler, payments, uow) = Build(availableCredit: 50000);

        var result = await handler.ExecuteAsync(Refund(30000, reason: "  "));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }
}
