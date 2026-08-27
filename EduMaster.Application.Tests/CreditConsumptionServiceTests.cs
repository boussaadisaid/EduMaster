using EduMaster.Application.Billing;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>خدمة استهلاك الزائدة (6.6 — ز-1): الحراس الثلاثة المبكرون + الكتابة بالاقتراح النقي بالترتيب + ختم اللحظة والمستخدم على كل سطر</summary>
public class CreditConsumptionServiceTests
{
    private static OpenChargeItem OpenCharge(int id, long amountCentimes, long allocatedCentimes = 0) =>
        new(id, ChargeKind.SessionBundle, "مصدر", amountCentimes, allocatedCentimes, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task NoCredit_WritesNothing()
    {
        var payments = new FakePaymentRepository { UnallocatedValue = 0 };
        var charges = new FakeChargeRepository { OpenToReturn = new List<OpenChargeItem> { OpenCharge(20, 30000) } };
        var service = new CreditConsumptionService(payments, charges);

        var written = await service.ConsumeForStudentAsync(1, DateTime.UtcNow, 7);

        Assert.Equal(0, written);
        Assert.Empty(payments.Allocations);
    }

    [Fact]
    public async Task NoOpenCharges_WritesNothing()
    {
        var payments = new FakePaymentRepository { UnallocatedValue = 5000 };
        payments.UnallocatedReceipts.Add(new UnallocatedReceiptRaw(5, 5000));
        var charges = new FakeChargeRepository { OpenToReturn = new List<OpenChargeItem>() };
        var service = new CreditConsumptionService(payments, charges);

        var written = await service.ConsumeForStudentAsync(1, DateTime.UtcNow, 7);

        Assert.Equal(0, written);
        Assert.Empty(payments.Allocations);
    }

    [Fact]
    public async Task NoFreeReceipts_WritesNothing_Defensive()   // زائدة > 0 بلا إيصال حرّ — مستحيلة رياضياً لكنها محروسة
    {
        var payments = new FakePaymentRepository { UnallocatedValue = 5000 };
        var charges = new FakeChargeRepository { OpenToReturn = new List<OpenChargeItem> { OpenCharge(20, 30000) } };
        var service = new CreditConsumptionService(payments, charges);

        var written = await service.ConsumeForStudentAsync(1, DateTime.UtcNow, 7);

        Assert.Equal(0, written);
        Assert.Empty(payments.Allocations);
    }

    [Fact]
    public async Task Consumption_WritesSuggestions_OldestFirst_AndCapped()
    {
        var payments = new FakePaymentRepository { UnallocatedValue = 60000 };
        payments.UnallocatedReceipts.Add(new UnallocatedReceiptRaw(5, 50000));
        payments.UnallocatedReceipts.Add(new UnallocatedReceiptRaw(8, 50000));
        var charges = new FakeChargeRepository { OpenToReturn = new List<OpenChargeItem> { OpenCharge(20, 30000), OpenCharge(21, 40000) } };
        var service = new CreditConsumptionService(payments, charges);

        var written = await service.ConsumeForStudentAsync(1, new DateTime(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc), 7);

        Assert.Equal(3, written);
        Assert.Equal(3, payments.Allocations.Count);
        Assert.Equal((5, 20, 30000L), (payments.Allocations[0].PaymentId, payments.Allocations[0].ChargeId, payments.Allocations[0].AmountCentimes));
        Assert.Equal((5, 21, 20000L), (payments.Allocations[1].PaymentId, payments.Allocations[1].ChargeId, payments.Allocations[1].AmountCentimes));
        Assert.Equal((8, 21, 10000L), (payments.Allocations[2].PaymentId, payments.Allocations[2].ChargeId, payments.Allocations[2].AmountCentimes));
    }

    [Fact]
    public async Task Consumption_StampsUtcNowAndUser_OnEveryLine()
    {
        var utcNow = new DateTime(2026, 8, 27, 12, 30, 0, DateTimeKind.Utc);
        var payments = new FakePaymentRepository { UnallocatedValue = 10000 };
        payments.UnallocatedReceipts.Add(new UnallocatedReceiptRaw(5, 10000));
        var charges = new FakeChargeRepository { OpenToReturn = new List<OpenChargeItem> { OpenCharge(20, 4000), OpenCharge(21, 6000) } };
        var service = new CreditConsumptionService(payments, charges);

        await service.ConsumeForStudentAsync(1, utcNow, 7);

        Assert.All(payments.Allocations, a =>
        {
            Assert.Equal(utcNow, a.CreatedAtUtc);
            Assert.Equal(7, a.CreatedByUserId);
        });
    }
}
