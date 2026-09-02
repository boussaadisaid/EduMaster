using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>تسجيل القبض (D-104…D-107): نجاح + فائض زائدة + كل الحُراس بلا كتابة عند الفشل</summary>
public sealed class RegisterPaymentHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 15, 0, 0, DateTimeKind.Utc);

    private static Domain.Billing.Charge BuildCharge(int id, int studentId, long amount, ChargeStatus status = ChargeStatus.Active) =>
        Domain.Billing.Charge.Load(
            id: id, studentId: studentId, kind: ChargeKind.SessionBundle, annualEnrollmentId: null, groupSessionPurchaseId: id * 10,
            originalAmountCentimes: amount, amountCentimes: amount,
            status: status, adjustmentNote: null, cancelledAtUtc: status == ChargeStatus.Cancelled ? Now : null,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static OpenChargeItem Open(int id, int studentId, long amount, long allocated) =>
        new(id, ChargeKind.SessionBundle, $"مستحق {id}", amount, allocated, Now);

    private static (RegisterPaymentHandler handler, FakePaymentRepository payments, FakeChargeRepository charges, FakeUnitOfWork uow) Build(
        IReadOnlyDictionary<int, Domain.Billing.Charge>? byId = null, IReadOnlyList<OpenChargeItem>? open = null)
    {
        var payments = new FakePaymentRepository { NextReceiptNo = 41 };
        var charges = new FakeChargeRepository
        {
            ChargesById = byId ?? new Dictionary<int, Domain.Billing.Charge>(),
            OpenToReturn = open ?? new List<OpenChargeItem>()
        };
        var uow = new FakeUnitOfWork();
        var treasuryAccounts = new FakeTreasuryAccountRepository();
        var handler = new RegisterPaymentHandler(payments, charges, new FakeClock(), new FakeCurrentUserService(),
            treasuryAccounts, uow, new CreditConsumptionService(payments, charges),
            NullLogger<RegisterPaymentHandler>.Instance);
        return (handler, payments, charges, uow);
    }

    private static RegisterPaymentRequest Pay(long amount, params PaymentAllocationInput[] allocations) =>
        new(StudentId: 2, PaidByPersonId: 9, TreasuryAccountId: 1, AmountCentimes: amount, PaidOn: new DateOnly(2026, 8, 23), Note: null, Allocations: allocations);

    [Fact]
    public async Task FullCoverage_WritesPaymentAndAllocation_InOneCommit_WithReceiptNo()
    {
        var charge = BuildCharge(5, studentId: 2, amount: 100000);
        var (handler, payments, _, uow) = Build(
            byId: new Dictionary<int, Domain.Billing.Charge> { [5] = charge },
            open: new List<OpenChargeItem> { Open(5, 2, 100000, 0) });

        var result = await handler.ExecuteAsync(Pay(100000, new PaymentAllocationInput(5, 100000)));

        Assert.True(result.IsSuccess);
        Assert.Equal(41, result.Value);                              // رقم الإيصال للـToast (D-105)
        var payment = Assert.Single(payments.Payments);
        Assert.Equal(2, payment.StudentId);
        Assert.Equal(9, payment.PaidByPersonId);
        Assert.Equal(100000, payment.AmountCentimes);
        var allocation = Assert.Single(payments.Allocations);
        Assert.Equal(payment.Id, allocation.PaymentId);              // معرّف المزيّف (SetId)
        Assert.Equal(5, allocation.ChargeId);
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task PartialAllocation_LeavesCredit_AndIsAllowed()   // D-107: زائدة دائنة مسموحة
    {
        var charge = BuildCharge(5, 2, 100000);
        var (handler, payments, _, uow) = Build(
            byId: new Dictionary<int, Domain.Billing.Charge> { [5] = charge },
            open: new List<OpenChargeItem> { Open(5, 2, 100000, 0) });

        var result = await handler.ExecuteAsync(Pay(50000, new PaymentAllocationInput(5, 30000)));

        Assert.True(result.IsSuccess);                               // 20000 زائدة تبقى على الدفعة
        Assert.Single(payments.Payments);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task PaymentWithoutAllocations_PureCredit_IsAllowed()
    {
        var (handler, payments, _, uow) = Build();

        var result = await handler.ExecuteAsync(Pay(80000));

        Assert.True(result.IsSuccess);
        Assert.Single(payments.Payments);
        Assert.Empty(payments.Allocations);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task NonPositiveAmount_ValidationError_BeforeAnyRead()
    {
        var (handler, payments, _, uow) = Build();

        var result = await handler.ExecuteAsync(Pay(0));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task FuturePaidOn_ValidationError()
    {
        var (handler, payments, _, uow) = Build();

        var result = await handler.ExecuteAsync(
            new RegisterPaymentRequest(2, null, 1, 50000, new DateOnly(2026, 8, 30), null, null));   // اليوم عند الساعة المزيّفة 2026-08-23

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task AllocationsExceedingAmount_ValidationError_WithoutWriting()
    {
        var charge = BuildCharge(5, 2, 100000);
        var (handler, payments, _, uow) = Build(
            byId: new Dictionary<int, Domain.Billing.Charge> { [5] = charge },
            open: new List<OpenChargeItem> { Open(5, 2, 100000, 0) });

        var result = await handler.ExecuteAsync(Pay(50000, new PaymentAllocationInput(5, 60000)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task AllocationBeyondRemaining_ValidationError()
    {
        var charge = BuildCharge(5, 2, 100000);
        var (handler, payments, _, uow) = Build(
            byId: new Dictionary<int, Domain.Billing.Charge> { [5] = charge },
            open: new List<OpenChargeItem> { Open(5, 2, 100000, 60000) });   // المتبقي 40000

        var result = await handler.ExecuteAsync(Pay(100000, new PaymentAllocationInput(5, 50000)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task AllocationToOtherStudentsCharge_ValidationError()
    {
        var charge = BuildCharge(5, studentId: 99, amount: 100000);   // لطالب آخر
        var (handler, payments, _, uow) = Build(
            byId: new Dictionary<int, Domain.Billing.Charge> { [5] = charge },
            open: new List<OpenChargeItem> { Open(5, 99, 100000, 0) });

        var result = await handler.ExecuteAsync(Pay(100000, new PaymentAllocationInput(5, 100000)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
    }

    [Fact]
    public async Task AllocationToCancelledCharge_BusinessRule()
    {
        var charge = BuildCharge(5, 2, 100000, ChargeStatus.Cancelled);
        var (handler, payments, _, uow) = Build(
            byId: new Dictionary<int, Domain.Billing.Charge> { [5] = charge },
            open: new List<OpenChargeItem>());   // المسوّى ليس ضمن المفتوحة

        var result = await handler.ExecuteAsync(Pay(100000, new PaymentAllocationInput(5, 100000)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task DuplicateChargeInAllocations_ValidationError()
    {
        var (handler, payments, _, uow) = Build();

        var result = await handler.ExecuteAsync(Pay(100000,
            new PaymentAllocationInput(5, 40000), new PaymentAllocationInput(5, 40000)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(payments.Payments);
        Assert.Equal(0, uow.BeganCount);
    }
}