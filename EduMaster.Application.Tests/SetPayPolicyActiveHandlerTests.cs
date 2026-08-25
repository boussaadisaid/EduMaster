using EduMaster.Application.Common;
using EduMaster.Application.Payroll;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>تفعيل/تعطيل السياسة: الخاملة بلا كتابة · التفعيل يمرّ بفحص فرادة النطاق · التعطيل دائم الجواز (روح D-45)</summary>
public sealed class SetPayPolicyActiveHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);   // = ساعة المزيّف الافتراضية

    private static PayPolicy Policy(int id, bool isActive) =>
        PayPolicy.Load(id, PayeeKind.Teacher, teacherId: 3, employeeId: null, classGroupId: null,
            PayPolicyKind.PerPresentStudent, rateCentimes: 20000, percentage: null,
            countsUnjustifiedAbsent: false, isActive: isActive,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static (SetPayPolicyActiveHandler handler, FakePayPolicyRepository policies, FakeUnitOfWork uow) Build(
        PayPolicy? policy, PayPolicy? activeExisting = null)
    {
        var policies = new FakePayPolicyRepository { EntityToReturn = policy, ActiveToReturn = activeExisting };
        var uow = new FakeUnitOfWork();
        var handler = new SetPayPolicyActiveHandler(policies, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<SetPayPolicyActiveHandler>.Instance);
        return (handler, policies, uow);
    }

    [Fact]
    public async Task PolicyMissing_NotFound()
    {
        var (handler, policies, uow) = Build(policy: null);

        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(7, true));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(policies.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task AlreadyActive_Activate_Succeeds_WithoutWrite()   // لا كتابة بلا معنى
    {
        var (handler, policies, uow) = Build(policy: Policy(7, isActive: true));

        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(7, true));

        Assert.True(result.IsSuccess);
        Assert.Empty(policies.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task AlreadyInactive_Deactivate_Succeeds_WithoutWrite()
    {
        var (handler, policies, uow) = Build(policy: Policy(7, isActive: false));

        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(7, false));

        Assert.True(result.IsSuccess);
        Assert.Empty(policies.Updated);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task Activation_FreeScope_Writes_AndCommits()
    {
        var (handler, policies, uow) = Build(policy: Policy(7, isActive: false), activeExisting: null);

        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(7, true));

        Assert.True(result.IsSuccess);
        var updated = Assert.Single(policies.Updated);
        Assert.True(updated.IsActive);
        Assert.Equal(Now, updated.UpdatedAtUtc);          // ختم الساعة المزيّفة
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task Activation_OccupiedScope_Conflict_NoWrite()
    {
        var (handler, policies, uow) = Build(policy: Policy(7, isActive: false), activeExisting: Policy(20, isActive: true));

        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(7, true));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);   // فعّالة أخرى على نفس النطاق — «عطّلها أولاً»
        Assert.Empty(policies.Updated);
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task Deactivation_AlwaysAllowed()
    {
        var (handler, policies, uow) = Build(policy: Policy(7, isActive: true));

        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(7, false));

        Assert.True(result.IsSuccess);
        var updated = Assert.Single(policies.Updated);
        Assert.False(updated.IsActive);
        Assert.Equal(1, uow.CommittedCount);
    }
}