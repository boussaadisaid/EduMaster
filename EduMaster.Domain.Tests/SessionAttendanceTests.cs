using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Scheduling;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>كيان سطر الحضور (D-93/D-101) — حُراس الإنشاء</summary>
public sealed class SessionAttendanceTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Valid_SetsFields()
    {
        var row = SessionAttendance.Create(10, 5, AttendanceStatus.Justified, "عذر طبي", Now, 1);

        Assert.Equal(10, row.ClassSessionId);
        Assert.Equal(5, row.ClassGroupEnrollmentId);
        Assert.Equal(AttendanceStatus.Justified, row.Status);
        Assert.Equal("عذر طبي", row.Note);
        Assert.Equal(Now, row.MarkedAtUtc);
    }

    [Fact]
    public void Create_NonPositiveSession_Throws()
    {
        Assert.Throws<DomainException>(() => SessionAttendance.Create(0, 5, AttendanceStatus.Present, null, Now, null));
    }

    [Fact]
    public void Create_NonPositiveEnrollment_Throws()
    {
        Assert.Throws<DomainException>(() => SessionAttendance.Create(10, 0, AttendanceStatus.Present, null, Now, null));
    }

    [Fact]
    public void Create_UndefinedStatus_Throws()
    {
        Assert.Throws<DomainException>(() => SessionAttendance.Create(10, 5, (AttendanceStatus)99, null, Now, null));
    }

    [Fact]
    public void Create_NoteOver200_Throws()
    {
        var longNote = new string('م', 201);

        Assert.Throws<DomainException>(() => SessionAttendance.Create(10, 5, AttendanceStatus.Present, longNote, Now, null));
    }
}