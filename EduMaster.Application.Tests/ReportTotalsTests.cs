using EduMaster.Application.Billing;
using EduMaster.Application.Reports;
using EduMaster.Domain.Enums;
using System;
using System.Collections.Generic;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>إجماليات تقارير 6.1 (D-127/ت-6): مشتقة من السطور لا مُخزَّنة — حركة القبض: قبض/صرف/مخصوص/غير مخصص/صافي · كشف الحساب: الرصيد على الفعّال فقط والملغى لا يُحسب (D-108/D-109)</summary>
public sealed class ReportTotalsTests
{
    private static PaymentListItem LogRow(int id, PaymentKind kind, long amount, long allocated = 0) =>
        new(id, 100 + id, kind, $"طالب {id}", null, amount, new DateTime(2026, 8, 25), null, allocated);

    private static StudentChargeItem Charge(int id, ChargeStatus status, long amount, long allocated) =>
        new(id, 2, ChargeKind.SessionBundle, $"مستحق {id}", amount, amount, status,
            status == ChargeStatus.Cancelled ? "إلغاء موثق" : null, new DateTime(2026, 8, 20), allocated);

    private static StudentPaymentLine Payment(int id, PaymentKind kind, long amount) =>
        new(id, 200 + id, kind, null, amount, new DateTime(2026, 8, 24), null, 0,
            new List<StudentPaymentAllocationLine>());

    [Fact]
    public void Movement_MixedKinds_TotalsAndNetDerived()
    {
        var report = new PaymentMovementReportItem(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 25),
            new List<PaymentListItem>
            {
                LogRow(1, PaymentKind.Receipt, 50000, allocated: 20000),
                LogRow(2, PaymentKind.Receipt, 30000, allocated: 30000),
                LogRow(3, PaymentKind.Refund, 10000),
            });

        Assert.Equal(2, report.ReceiptsCount);
        Assert.Equal(80000, report.ReceiptsTotalCentimes);
        Assert.Equal(1, report.RefundsCount);
        Assert.Equal(10000, report.RefundsTotalCentimes);
        Assert.Equal(50000, report.AllocatedTotalCentimes);
        Assert.Equal(30000, report.UnallocatedTotalCentimes);   // مقبوض − مخصوص — زائدة متولدة (D-107)
        Assert.Equal(70000, report.NetCentimes);                // قبض − صرف
    }

    [Fact]
    public void Movement_EmptyPeriod_AllZeros()
    {
        var report = new PaymentMovementReportItem(new DateOnly(2026, 8, 25), new DateOnly(2026, 8, 25),
            new List<PaymentListItem>());

        Assert.Equal(0, report.ReceiptsCount);
        Assert.Equal(0, report.ReceiptsTotalCentimes);
        Assert.Equal(0, report.RefundsTotalCentimes);
        Assert.Equal(0, report.AllocatedTotalCentimes);
        Assert.Equal(0, report.UnallocatedTotalCentimes);
        Assert.Equal(0, report.NetCentimes);
    }

    [Fact]
    public void Statement_Balance_ActiveChargesOnly_CancelledExcluded()
    {
        var statement = new StudentStatementItem(
            new List<StudentChargeItem>
            {
                Charge(7, ChargeStatus.Active, 200000, allocated: 50000),    // متبقٍّ 150000
                Charge(9, ChargeStatus.Cancelled, 100000, allocated: 0),     // ملغى موثق — لا يُحسب (D-108)
            },
            new List<StudentPaymentLine>(),
            CreditCentimes: 0);

        Assert.Equal(150000, statement.BalanceCentimes);
    }

    [Fact]
    public void Statement_ReceiptsAndRefundsTotals_ByKind()
    {
        var statement = new StudentStatementItem(
            new List<StudentChargeItem>(),
            new List<StudentPaymentLine>
            {
                Payment(1, PaymentKind.Receipt, 80000),
                Payment(2, PaymentKind.Receipt, 40000),
                Payment(3, PaymentKind.Refund, 15000),
            },
            CreditCentimes: 45000);

        Assert.Equal(120000, statement.ReceiptsTotalCentimes);
        Assert.Equal(15000, statement.RefundsTotalCentimes);
        Assert.Equal(45000, statement.CreditCentimes);
    }
}