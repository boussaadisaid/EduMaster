using EduMaster.Domain.Common;
using EduMaster.Domain.Employees;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>كيان الموظف (D-115) — حُراس الإنشاء والتعديل كما سُلّمت في ب-1 حرفاً</summary>
public sealed class EmployeeTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Valid_SetsFields_AndTrimsJobTitle()
    {
        var employee = Employee.Create(5, "  محاسب  ", "  مناوبة صباحية  ", Now, 1);

        Assert.Equal(5, employee.PersonId);
        Assert.Equal("محاسب", employee.JobTitle);
        Assert.Equal("مناوبة صباحية", employee.Notes);
        Assert.Equal(Now, employee.CreatedAtUtc);
        Assert.Equal(1, employee.CreatedByUserId);
        Assert.Null(employee.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyJobTitle_Throws(string? jobTitle)
    {
        Assert.Throws<DomainException>(() => Employee.Create(5, jobTitle!, null, Now, null));
    }

    [Fact]
    public void Create_JobTitleOver100_Throws()
    {
        var longTitle = new string('م', 101);

        Assert.Throws<DomainException>(() => Employee.Create(5, longTitle, null, Now, null));
    }

    [Fact]
    public void Create_NotesOver500_Throws()
    {
        var longNotes = new string('م', 501);

        Assert.Throws<DomainException>(() => Employee.Create(5, "محاسب", longNotes, Now, null));
    }

    [Fact]
    public void Create_NonPositivePerson_Throws()
    {
        Assert.Throws<DomainException>(() => Employee.Create(0, "محاسب", null, Now, null));
    }

    [Fact]
    public void Update_ChangesJobTitleAndNotes_AndStampsAudit()
    {
        var employee = Employee.Load(7, 5, "محاسب", null, Now, 1, null, null);
        var later = Now.AddHours(2);

        employee.Update("مشرف", "ترقية", later, 2);

        Assert.Equal("مشرف", employee.JobTitle);
        Assert.Equal("ترقية", employee.Notes);
        Assert.Equal(5, employee.PersonId);              // الهوية لا تتغير
        Assert.Equal(later, employee.UpdatedAtUtc);
        Assert.Equal(2, employee.UpdatedByUserId);
    }

    [Fact]
    public void Load_AssignsId()
    {
        var employee = Employee.Load(7, 5, "محاسب", null, Now, 1, null, null);

        Assert.Equal(7, employee.Id);
    }
}