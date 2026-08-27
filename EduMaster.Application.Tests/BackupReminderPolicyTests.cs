using EduMaster.Application.Backup;
using System;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>سياسة تذكير النسخ (6.5 — ن-4/ن-6): أبداً ← تذكير · اليوم نفسه بلا · 7 أيام تماماً بلا (الحد «أكثر من») · أكثر من 7 ← تذكير</summary>
public sealed class BackupReminderPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NeverBackedUp_Reminds()
        => Assert.True(BackupReminderPolicy.ShouldRemind(null, Now));

    [Fact]
    public void SameDay_DoesNotRemind()
        => Assert.False(BackupReminderPolicy.ShouldRemind(Now.AddHours(-3), Now));

    [Fact]
    public void ExactlySevenDays_DoesNotRemind()   // الحد: «أكثر من» سبعة أيام
        => Assert.False(BackupReminderPolicy.ShouldRemind(Now.AddDays(-7), Now));

    [Fact]
    public void MoreThanSevenDays_Reminds()
        => Assert.True(BackupReminderPolicy.ShouldRemind(Now.AddDays(-7).AddHours(-1), Now));
}
