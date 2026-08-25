using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>يوم عمل الموظف (D-115) — كتابة فقط: التصحيح = حذف وإعادة</summary>
public sealed class WorkLogEntryTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Day = new(2026, 8, 23);

    [Fact]
    public void Create_Valid_SetsFields_AndTrimsNote()
    {
        var entry = WorkLogEntry.Create(5, Day, "  تنظيف عميق  ", Now, 1);

        Assert.Equal(5, entry.EmployeeId);
        Assert.Equal(Day, entry.WorkDate);
        Assert.Equal("تنظيف عميق", entry.Note);
        Assert.Equal(Now, entry.CreatedAtUtc);
        Assert.Equal(1, entry.CreatedByUserId);
    }

    [Fact]
    public void Create_NonPositiveEmployee_Throws()
    {
        Assert.Throws<DomainException>(() => WorkLogEntry.Create(0, Day, null, Now, null));
    }

    [Fact]
    public void Create_NoteOver200_Throws()
    {
        var longNote = new string('م', 201);

        Assert.Throws<DomainException>(() => WorkLogEntry.Create(5, Day, longNote, Now, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyNote_BecomesNull(string? note)
    {
        var entry = WorkLogEntry.Create(5, Day, note, Now, null);

        Assert.Null(entry.Note);
    }

    [Fact]
    public void Load_AssignsId()
    {
        var entry = WorkLogEntry.Load(7, 5, Day, null, Now, 1);

        Assert.Equal(7, entry.Id);
    }
}