using EduMaster.Domain.Billing;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>كيان المستحق (D-103/D-108): مصنعا المصدر + التسوية الموثقة (إلغاء/تخفيض) بلا حذف (D-109)</summary>
public sealed class ChargeTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);

    // ---------- المصنعان وإقران المصدر (D-103) ----------
    [Fact]
    public void CreateForRegistrationFee_LinksAnnualEnrollmentOnly()
    {
        var charge = Charge.CreateForRegistrationFee(studentId: 2, annualEnrollmentId: 7, amountCentimes: 100000, utcNow: Now, createdByUserId: 1);

        Assert.Equal(ChargeKind.RegistrationFee, charge.Kind);
        Assert.Equal(7, charge.AnnualEnrollmentId);
        Assert.Null(charge.GroupSessionPurchaseId);
        Assert.Equal(100000, charge.OriginalAmountCentimes);
        Assert.Equal(100000, charge.AmountCentimes);
        Assert.Equal(ChargeStatus.Active, charge.Status);
    }

    [Fact]
    public void CreateForSessionBundle_LinksPurchaseOnly()
    {
        var charge = Charge.CreateForSessionBundle(studentId: 2, groupSessionPurchaseId: 9, amountCentimes: 140000, utcNow: Now, createdByUserId: 1);

        Assert.Equal(ChargeKind.SessionBundle, charge.Kind);
        Assert.Equal(9, charge.GroupSessionPurchaseId);
        Assert.Null(charge.AnnualEnrollmentId);
    }

    [Fact]
    public void Create_ZeroAmount_Throws()   // الصفر لا يولّد مستحقاً (D-103)
    {
        Assert.Throws<DomainException>(() => Charge.CreateForRegistrationFee(2, 7, 0, Now, null));
    }

    [Fact]
    public void Create_NonPositiveStudent_Throws()
    {
        Assert.Throws<DomainException>(() => Charge.CreateForSessionBundle(0, 9, 140000, Now, null));
    }

    // ---------- الإلغاء الموثق (D-108) ----------
    [Fact]
    public void Cancel_Active_SetsStatusReasonAndDate()
    {
        var charge = Charge.CreateForRegistrationFee(2, 7, 100000, Now, 1);

        charge.Cancel("خطأ إدخال", Now, 1);

        Assert.Equal(ChargeStatus.Cancelled, charge.Status);
        Assert.Equal("خطأ إدخال", charge.AdjustmentNote);
        Assert.Equal(Now, charge.CancelledAtUtc);
        Assert.Equal(100000, charge.AmountCentimes);   // المبالغ تبقى للتدقيق
    }

    [Fact]
    public void Cancel_WithoutReason_Throws()
    {
        var charge = Charge.CreateForRegistrationFee(2, 7, 100000, Now, 1);

        Assert.Throws<DomainException>(() => charge.Cancel("   ", Now, 1));
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var charge = Charge.CreateForRegistrationFee(2, 7, 100000, Now, 1);
        charge.Cancel("أولاً", Now, 1);

        Assert.Throws<DomainException>(() => charge.Cancel("ثانياً", Now, 1));
    }

    // ---------- التخفيض الموثق (D-108) ----------
    [Fact]
    public void Reduce_ToLowerAmount_Works_AndKeepsOriginal()
    {
        var charge = Charge.CreateForSessionBundle(2, 9, 140000, Now, 1);

        charge.Reduce(60000, "تخفيض متفق عليه", Now, 1);

        Assert.Equal(60000, charge.AmountCentimes);
        Assert.Equal(140000, charge.OriginalAmountCentimes);   // الأصلي محفوظ للتدقيق
        Assert.Equal("تخفيض متفق عليه", charge.AdjustmentNote);
        Assert.Equal(ChargeStatus.Active, charge.Status);
    }

    [Fact]
    public void Reduce_ToZero_IsAllowed()   // إعفاء ما بعد الاتفاق
    {
        var charge = Charge.CreateForSessionBundle(2, 9, 140000, Now, 1);

        charge.Reduce(0, "إعفاء لاحق", Now, 1);

        Assert.Equal(0, charge.AmountCentimes);
    }

    [Theory]
    [InlineData(140000)]   // مساوٍ = ليس تخفيضاً
    [InlineData(150000)]   // أكبر = زيادة ممنوعة من هذا الباب
    public void Reduce_ToSameOrHigher_Throws(long newAmount)
    {
        var charge = Charge.CreateForSessionBundle(2, 9, 140000, Now, 1);

        Assert.Throws<DomainException>(() => charge.Reduce(newAmount, "سبب", Now, 1));
    }

    [Fact]
    public void Reduce_Cancelled_Throws()
    {
        var charge = Charge.CreateForSessionBundle(2, 9, 140000, Now, 1);
        charge.Cancel("أُلغي", Now, 1);

        Assert.Throws<DomainException>(() => charge.Reduce(60000, "سبب", Now, 1));
    }
}