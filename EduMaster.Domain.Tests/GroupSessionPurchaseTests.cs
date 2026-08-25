using EduMaster.Domain.Common;
using EduMaster.Domain.Scheduling;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>كيان شراء الحصص (D-91/D-96) — حُراس الإنشاء وحقوله</summary>
public sealed class GroupSessionPurchaseTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Valid_SetsFields_AndTrimsNote()
    {
        var purchase = GroupSessionPurchase.Create(5, 4, "  حزمة شهر  ", Now, 1);

        Assert.Equal(5, purchase.ClassGroupEnrollmentId);
        Assert.Equal(4, purchase.SessionsCount);
        Assert.Equal("حزمة شهر", purchase.Note);
        Assert.Equal(Now, purchase.PurchasedAtUtc);
        Assert.Equal(Now, purchase.CreatedAtUtc);
        Assert.Equal(1, purchase.CreatedByUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyNote_BecomesNull(string? note)
    {
        var purchase = GroupSessionPurchase.Create(5, 4, note, Now, null);

        Assert.Null(purchase.Note);
    }

    [Fact]
    public void Create_NonPositiveEnrollment_Throws()
    {
        Assert.Throws<DomainException>(() => GroupSessionPurchase.Create(0, 4, null, Now, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_NonPositiveCount_Throws(int count)
    {
        Assert.Throws<DomainException>(() => GroupSessionPurchase.Create(5, count, null, Now, null));
    }

    [Fact]
    public void Create_NoteOver200_Throws()
    {
        var longNote = new string('م', 201);

        Assert.Throws<DomainException>(() => GroupSessionPurchase.Create(5, 4, longNote, Now, null));
    }

    [Fact]
    public void Load_AssignsId()
    {
        var purchase = GroupSessionPurchase.Load(7, 5, 4, Now, null, Now, 1, null, null);

        Assert.Equal(7, purchase.Id);
    }
}