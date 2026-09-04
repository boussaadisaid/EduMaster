using EduMaster.Application.Enrollments;
using EduMaster.Domain.Enums;
using System;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>الرصيد = مشترى − مخصوم (D-91) · السالب مسموح وموسوم (D-92)</summary>
public sealed class StudentGroupEnrollmentItemTests
{
    private static StudentGroupEnrollmentItem Build(int purchased, int consumed, EnrollmentStatus status = EnrollmentStatus.Active) =>
        new(1, 10, "فوج", "مادة", "2025-2026", status, 35000, new DateTime(2026, 8, 20), purchased, consumed);

    [Fact]
    public void Balance_IsPurchasedMinusConsumed()
    {
        Assert.Equal(3, Build(purchased: 8, consumed: 5).Balance);
    }

    [Fact]
    public void Balance_CanGoNegative_AndIsFlagged()
    {
        var item = Build(purchased: 2, consumed: 5);

        Assert.Equal(-3, item.Balance);
        Assert.True(item.IsNegativeBalance);
    }

    [Fact]
    public void Balance_ZeroOrPositive_IsNotFlagged()
    {
        Assert.False(Build(4, 4).IsNegativeBalance);
        Assert.False(Build(5, 4).IsNegativeBalance);
    }

    [Fact]
    public void Balance_IncludesTransfersInAndOut()
    {
        var item = new StudentGroupEnrollmentItem(1, 10, "فوج", "مادة", "2025-2026",
            EnrollmentStatus.Active, 35000, new DateTime(2026, 8, 20),
            PurchasedSessions: 3, ConsumedSessions: 2)
        {
            TransferredInSessions = 7,
            TransferredOutSessions = 5
        };

        Assert.Equal(3, item.Balance);
    }

    [Fact]
    public void StatusText_Arabic()
    {
        Assert.Equal("نشط", Build(0, 0, EnrollmentStatus.Active).StatusText);
        Assert.Equal("منسحب", Build(0, 0, EnrollmentStatus.Withdrawn).StatusText);
    }
}