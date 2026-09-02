
using EduMaster.Domain.Billing;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>كيان الإيصال (D-104/D-105): وثيقة لا تُعدَّل — العكس بإيصال صرف (D-108)</summary>
public sealed class PaymentTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 8, 23);

    [Fact]
    public void Create_Valid_SetsFields_AndTrimsNote()
    {
        var payment = Payment.Create(2, 9, 1, PaymentKind.Receipt, 100000, Today, "  دفعة شهر  ", 1, Now, 1);

        Assert.Equal(2, payment.StudentId);
        Assert.Equal(9, payment.PaidByPersonId);        // الولي الدافع (D-104)
        Assert.Equal(PaymentKind.Receipt, payment.Kind);
        Assert.Equal(100000, payment.AmountCentimes);
        Assert.Equal(Today, payment.PaidOn);
        Assert.Equal(1, payment.ReceiptNo);
        Assert.Equal("دفعة شهر", payment.Note);
    }

    [Fact]
    public void Create_WithoutPayer_IsAllowed()   // الطالب نفسه أو غير موسوم
    {
        var payment = Payment.Create(2, null, 1, PaymentKind.Receipt, 50000, Today, null, 1, Now, null);

        Assert.Null(payment.PaidByPersonId);
        Assert.Null(payment.Note);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveAmount_Throws(long amount)
    {
        Assert.Throws<DomainException>(() => Payment.Create(2, null, 1, PaymentKind.Receipt, amount, Today, null, 1, Now, null));
    }

    [Fact]
    public void Create_NonPositiveReceiptNo_Throws()
    {
        Assert.Throws<DomainException>(() => Payment.Create(2, null, 1, PaymentKind.Receipt, 50000, Today, null, 0, Now, null));
    }

    [Fact]
    public void Create_NonPositiveStudent_Throws()
    {
        Assert.Throws<DomainException>(() => Payment.Create(0, null, 1, PaymentKind.Receipt, 50000, Today, null, 1, Now, null));
    }

    [Fact]
    public void Create_NoteOver200_Throws()
    {
        var longNote = new string('م', 201);

        Assert.Throws<DomainException>(() => Payment.Create(2, null, 1, PaymentKind.Receipt, 50000, Today, longNote, 1, Now, null));
    }

    [Fact]
    public void Allocation_Create_Valid()
    {
        var allocation = PaymentAllocation.Create(7, 5, 60000, Now, 1);

        Assert.Equal(7, allocation.PaymentId);
        Assert.Equal(5, allocation.ChargeId);
        Assert.Equal(60000, allocation.AmountCentimes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Allocation_NonPositiveAmount_Throws(long amount)
    {
        Assert.Throws<DomainException>(() => PaymentAllocation.Create(7, 5, amount, Now, null));
    }
}
