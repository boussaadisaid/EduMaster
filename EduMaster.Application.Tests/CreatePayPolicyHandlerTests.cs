using EduMaster.Application.Common;
using EduMaster.Application.Payroll;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>إنشاء سياسة أجر (D-113/D-114): نجاح للفريقين + حُراس وجود المستفيد/الفوج + فرادة الفعّالة على النطاق + قواعد الكيان — كل فشل بلا كتابة</summary>
public sealed class CreatePayPolicyHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

    private static Domain.Teachers.Teacher PlantTeacher(int id = 3) =>
        Domain.Teachers.Teacher.Load(id, personId: 10, specialty: null, notes: null,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static Domain.Employees.Employee PlantEmployee(int id = 4) =>
        Domain.Employees.Employee.Load(id, personId: 12, jobTitle: "منظفة", notes: null,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static Domain.ClassGroups.ClassGroup PlantGroup(int id = 9) =>
        Domain.ClassGroups.ClassGroup.Load(id, academicYearId: 1, levelId: 1, subjectId: 1,
            teacherId: 3, roomId: null, name: "رياضيات — ثالثة ثانوي", capacity: null, isActive: true,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static PayPolicy ExistingActive(int id = 20) =>
        PayPolicy.Load(id, PayeeKind.Teacher, teacherId: 3, employeeId: null, classGroupId: null,
            PayPolicyKind.PerPresentStudent, rateCentimes: 20000, percentage: null,
            countsUnjustifiedAbsent: false, isActive: true,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static (CreatePayPolicyHandler handler, FakePayPolicyRepository policies, FakeUnitOfWork uow) Build(
        bool withTeacher = true, bool withEmployee = true, bool withGroup = true, PayPolicy? activeExisting = null)
    {
        var policies = new FakePayPolicyRepository { ActiveToReturn = activeExisting };
        var teachers = new FakeTeacherRepository { EntityToReturn = withTeacher ? PlantTeacher() : null };
        var employees = new FakeEmployeeRepository { EntityToReturn = withEmployee ? PlantEmployee() : null };
        var groups = new FakeClassGroupRepository { EntityToReturn = withGroup ? PlantGroup() : null };
        var uow = new FakeUnitOfWork();
        var handler = new CreatePayPolicyHandler(policies, teachers, employees, groups,
            new FakeClock(), new FakeCurrentUserService(), uow, NullLogger<CreatePayPolicyHandler>.Instance);
        return (handler, policies, uow);
    }

    private static CreatePayPolicyRequest TeacherDefault(long rate = 20000) =>
        new(PayeeKind.Teacher, TeacherId: 3, EmployeeId: null, ClassGroupId: null,
            PayPolicyKind.PerPresentStudent, rate, null, false);

    [Fact]
    public async Task TeacherDefault_Valid_WritesAndCommits()
    {
        var (handler, policies, uow) = Build();

        var result = await handler.ExecuteAsync(TeacherDefault());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);                   // معرّف المزيّف (SetId)
        var policy = Assert.Single(policies.Added);
        Assert.Equal(PayeeKind.Teacher, policy.PayeeKind);
        Assert.Equal(3, policy.TeacherId);
        Assert.Equal(PayPolicyKind.PerPresentStudent, policy.Kind);
        Assert.Equal(20000, policy.RateCentimes);
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task EmployeePerMonth_Valid_WritesAndCommits()
    {
        var (handler, policies, uow) = Build();

        var result = await handler.ExecuteAsync(
            new CreatePayPolicyRequest(PayeeKind.Employee, null, EmployeeId: 4, null,
                PayPolicyKind.PerMonth, 1500000, null, false));

        Assert.True(result.IsSuccess);
        var policy = Assert.Single(policies.Added);
        Assert.Equal(PayeeKind.Employee, policy.PayeeKind);
        Assert.Equal(4, policy.EmployeeId);
        Assert.Equal(PayPolicyKind.PerMonth, policy.Kind);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task TeacherMissing_NotFound_NoWrite()
    {
        var (handler, policies, uow) = Build(withTeacher: false);

        var result = await handler.ExecuteAsync(TeacherDefault());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task EmployeeMissing_NotFound_NoWrite()
    {
        var (handler, policies, uow) = Build(withEmployee: false);

        var result = await handler.ExecuteAsync(
            new CreatePayPolicyRequest(PayeeKind.Employee, null, EmployeeId: 4, null,
                PayPolicyKind.PerDay, 100000, null, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task GroupMissing_NotFound_NoWrite()
    {
        var (handler, policies, uow) = Build(withGroup: false);

        var result = await handler.ExecuteAsync(
            new CreatePayPolicyRequest(PayeeKind.Teacher, TeacherId: 3, null, ClassGroupId: 9,
                PayPolicyKind.PerHour, 150000, null, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task ActiveDefaultOnSameScope_Conflict_NoWrite()
    {
        var (handler, policies, uow) = Build(activeExisting: ExistingActive());

        var result = await handler.ExecuteAsync(TeacherDefault());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);   // «عدّلها أو عطّلها أولاً» — الفهرس المفلتر يحرس خلفاً
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task ActiveOverrideOnSameGroup_Conflict_NoWrite()
    {
        var (handler, policies, uow) = Build(activeExisting: ExistingActive());

        var result = await handler.ExecuteAsync(
            new CreatePayPolicyRequest(PayeeKind.Teacher, TeacherId: 3, null, ClassGroupId: 9,
                PayPolicyKind.PerHour, 150000, null, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task InvalidPayeeKind_Validation_BeforeAnyWrite()
    {
        var (handler, policies, uow) = Build();

        var result = await handler.ExecuteAsync(
            new CreatePayPolicyRequest((PayeeKind)9, TeacherId: 3, null, null,
                PayPolicyKind.PerHour, 150000, null, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task EntityRuleMismatch_Validation_NoCommit()
    {
        var (handler, policies, uow) = Build();

        var result = await handler.ExecuteAsync(
            new CreatePayPolicyRequest(PayeeKind.Teacher, TeacherId: 3, null, null,
                PayPolicyKind.Percentage, 5000, 50m, false));   // نسبة مع قيمة ثابتة — يكسر قاعدة الكيان

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(policies.Added);
        Assert.Equal(0, uow.CommittedCount);
    }
}