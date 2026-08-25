using EduMaster.Application.Common;
using EduMaster.Application.Payroll;
using EduMaster.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>تسجيل يوم عمل (D-115): نجاح بكتابة وCommit + لا تاريخ مستقبل + لا يوم مكرر + كل فشل بلا كتابة</summary>
public sealed class AddWorkLogDayHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);   // ساعة المزيّف الافتراضية — Today = 2026-08-23

    private static Domain.Employees.Employee PlantEmployee(int id = 4) =>
        Domain.Employees.Employee.Load(id, personId: 12, jobTitle: "منظفة", notes: null,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static (AddWorkLogDayHandler handler, FakeEmployeeWorkLogRepository workLog, FakeUnitOfWork uow) Build(
        bool withEmployee = true, IReadOnlyList<WorkLogItem>? sameDay = null)
    {
        var employees = new FakeEmployeeRepository { EntityToReturn = withEmployee ? PlantEmployee() : null };
        var workLog = new FakeEmployeeWorkLogRepository { ItemsToReturn = sameDay ?? new List<WorkLogItem>() };
        var uow = new FakeUnitOfWork();
        var handler = new AddWorkLogDayHandler(employees, workLog, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<AddWorkLogDayHandler>.Instance);
        return (handler, workLog, uow);
    }

    [Fact]
    public async Task ValidDay_WritesEntry_InOneCommit()
    {
        var (handler, workLog, uow) = Build();
        var day = new DateOnly(2026, 8, 22);

        var result = await handler.ExecuteAsync(new AddWorkLogDayRequest(4, day, "  تنظيف  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);                   // معرّف المزيّف (SetId)
        var entry = Assert.Single(workLog.Added);
        Assert.Equal(4, entry.EmployeeId);
        Assert.Equal(day, entry.WorkDate);
        Assert.Equal("تنظيف", entry.Note);
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task EmployeeMissing_NotFound_BeforeAnyWrite()
    {
        var (handler, workLog, uow) = Build(withEmployee: false);

        var result = await handler.ExecuteAsync(new AddWorkLogDayRequest(99, new DateOnly(2026, 8, 22), null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(workLog.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task FutureDay_Validation_NoWrite()
    {
        var (handler, workLog, uow) = Build();

        var result = await handler.ExecuteAsync(new AddWorkLogDayRequest(4, new DateOnly(2026, 8, 30), null));   // اليوم عند الساعة المزيّفة 2026-08-23

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(workLog.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task DuplicateDay_Conflict_NoWrite()
    {
        var sameDay = new List<WorkLogItem> { new(1, 4, new DateTime(2026, 8, 22), null) };
        var (handler, workLog, uow) = Build(sameDay: sameDay);

        var result = await handler.ExecuteAsync(new AddWorkLogDayRequest(4, new DateOnly(2026, 8, 22), null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);   // «احذفه أولاً إن أردت تصحيحه» — التصحيح = حذف + إعادة
        Assert.Empty(workLog.Added);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task LongNote_DomainValidation_NoCommit()
    {
        var (handler, workLog, uow) = Build();
        var longNote = new string('م', 201);

        var result = await handler.ExecuteAsync(new AddWorkLogDayRequest(4, new DateOnly(2026, 8, 22), longNote));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);   // قاعدة الكيان (≤200)
        Assert.Empty(workLog.Added);
        Assert.Equal(0, uow.CommittedCount);
    }
}