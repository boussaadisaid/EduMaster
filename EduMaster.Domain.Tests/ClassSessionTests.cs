using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Scheduling;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>
/// كيان الحصة (D-90) — حُراس دورة الحياة + لقطة الأستاذ (D-117).
/// دين مؤجل منذ 3.1 يُسدَّد هنا: الكيان صار بحوزة المساعد حرفياً (وصفة الإغلاق — ب-4).
/// </summary>
public sealed class ClassSessionTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Starts = new(2026, 8, 29, 8, 0, 0);

    private static ClassSession NewScheduled() =>
        ClassSession.Create(classGroupId: 5, sourceScheduleId: null, startsAt: Starts,
            durationMinutes: 120, topic: null, utcNow: Now, createdByUserId: 1);

    [Fact]
    public void Create_Valid_StartsScheduled_WithoutTeacherNorSource()
    {
        var session = NewScheduled();

        Assert.Equal(5, session.ClassGroupId);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
        Assert.Null(session.TeacherId);                  // اللقطة تُملأ عند الإقامة فقط (D-117)
        Assert.Null(session.SourceScheduleId);           // بلا مصدر = استثنائية (D-87)
        Assert.Null(session.CancelledAtUtc);
        Assert.Equal(Now, session.CreatedAtUtc);
    }

    [Fact]
    public void Create_TrimsTopic()
    {
        var session = ClassSession.Create(5, null, Starts, 120, "  مراجعة عامة  ", Now, 1);

        Assert.Equal("مراجعة عامة", session.Topic);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyTopic_BecomesNull(string? topic)
    {
        var session = ClassSession.Create(5, null, Starts, 120, topic, Now, 1);

        Assert.Null(session.Topic);
    }

    [Fact]
    public void Create_NonPositiveGroup_Throws()
    {
        Assert.Throws<DomainException>(() => ClassSession.Create(0, null, Starts, 120, null, Now, null));
    }

    [Fact]
    public void Create_ZeroSourceScheduleId_Throws()
    {
        Assert.Throws<DomainException>(() => ClassSession.Create(5, 0, Starts, 120, null, Now, null));
    }

    [Fact]
    public void Create_NullSourceSchedule_IsAdHoc_Ok()
    {
        var session = ClassSession.Create(5, null, Starts, 120, null, Now, null);

        Assert.Null(session.SourceScheduleId);
    }

    [Fact]
    public void Load_ZeroTeacherId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            ClassSession.Load(7, 5, null, teacherId: 0, Starts, 120, SessionStatus.Scheduled, null, null, Now, 1, null, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Create_DurationOutOfRange_Throws(int duration)
    {
        Assert.Throws<DomainException>(() => ClassSession.Create(5, null, Starts, duration, null, Now, null));
    }

    [Fact]
    public void Create_TopicOver200_Throws()
    {
        var longTopic = new string('م', 201);

        Assert.Throws<DomainException>(() => ClassSession.Create(5, null, Starts, 120, longTopic, Now, null));
    }

    [Fact]
    public void MarkHeld_Scheduled_BecomesHeld_SnapshottingTeacher()   // D-117
    {
        var session = NewScheduled();
        var heldAt = Now.AddHours(1);

        session.MarkHeld(3, heldAt, 2);

        Assert.Equal(SessionStatus.Held, session.Status);
        Assert.Equal(3, session.TeacherId);              // لقطة أستاذ الفوج لحظة الإقامة
        Assert.Equal(heldAt, session.UpdatedAtUtc);
        Assert.Equal(2, session.UpdatedByUserId);
    }

    [Fact]
    public void MarkHeld_NullTeacher_SnapshotsNull()     // فارغ = بلا أستاذ مسند
    {
        var session = NewScheduled();

        session.MarkHeld(null, Now, 1);

        Assert.Equal(SessionStatus.Held, session.Status);
        Assert.Null(session.TeacherId);
    }

    [Fact]
    public void MarkHeld_Twice_KeepsFirstSnapshot()      // المال يُنسب لمن أقام فعلاً (D-117)
    {
        var session = NewScheduled();
        var heldAt = Now.AddHours(1);

        session.MarkHeld(3, heldAt, 2);
        session.MarkHeld(9, heldAt.AddHours(1), 7);      // مُقامة سابقاً — خاملة، لا تُمس اللقطة

        Assert.Equal(3, session.TeacherId);
        Assert.Equal(heldAt, session.UpdatedAtUtc);
        Assert.Equal(2, session.UpdatedByUserId);
    }

    [Fact]
    public void MarkHeld_Cancelled_Throws()
    {
        var session = NewScheduled();
        session.Cancel(Now, 1);

        Assert.Throws<DomainException>(() => session.MarkHeld(3, Now, 1));
    }

    [Fact]
    public void Cancel_Scheduled_BecomesCancelled_AndStamps()
    {
        var session = NewScheduled();
        var cancelledAt = Now.AddHours(1);

        session.Cancel(cancelledAt, 2);

        Assert.Equal(SessionStatus.Cancelled, session.Status);
        Assert.Equal(cancelledAt, session.CancelledAtUtc);
        Assert.Equal(2, session.UpdatedByUserId);
    }

    [Fact]
    public void Cancel_Twice_SecondIsNoOp()
    {
        var session = NewScheduled();
        var cancelledAt = Now.AddHours(1);

        session.Cancel(cancelledAt, 2);
        session.Cancel(cancelledAt.AddHours(1), 7);      // ملغاة سابقاً — خاملة

        Assert.Equal(cancelledAt, session.CancelledAtUtc);
        Assert.Equal(2, session.UpdatedByUserId);
    }

    [Fact]
    public void Cancel_Held_Throws()                     // المُقامة لا تُلغى — الحضور سُجّل فيها (D-90)
    {
        var session = NewScheduled();
        session.MarkHeld(3, Now, 1);

        Assert.Throws<DomainException>(() => session.Cancel(Now, 1));
    }

    [Fact]
    public void UpdateTopic_Scheduled_Updates_AndStamps()
    {
        var session = NewScheduled();
        var later = Now.AddHours(1);

        session.UpdateTopic("مراجعة", later, 2);

        Assert.Equal("مراجعة", session.Topic);
        Assert.Equal(later, session.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateTopic_Held_Throws()
    {
        var session = NewScheduled();
        session.MarkHeld(3, Now, 1);

        Assert.Throws<DomainException>(() => session.UpdateTopic("متأخر", Now, 1));
    }

    [Fact]
    public void UpdateTopic_Cancelled_Throws()
    {
        var session = NewScheduled();
        session.Cancel(Now, 1);

        Assert.Throws<DomainException>(() => session.UpdateTopic("متأخر", Now, 1));
    }

    [Fact]
    public void Load_AssignsId_AndTeacherId()
    {
        var session = ClassSession.Load(7, 5, sourceScheduleId: 2, teacherId: 3, Starts, 120,
            SessionStatus.Held, null, null, Now, 1, null, null);

        Assert.Equal(7, session.Id);
        Assert.Equal(2, session.SourceScheduleId);
        Assert.Equal(3, session.TeacherId);
    }
}