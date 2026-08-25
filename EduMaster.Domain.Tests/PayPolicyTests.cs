using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

/// <summary>سياسة الأجر الموحّدة (D-113/D-114) — اتساق النوع مع المستفيد + قاعدة «قيمة أو نسبة — واحدة فقط» + ثبات الهوية</summary>
public sealed class PayPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);

    private static PayPolicy TeacherDefault() =>
        PayPolicy.Create(PayeeKind.Teacher, teacherId: 3, employeeId: null, classGroupId: null,
            PayPolicyKind.PerPresentStudent, rateCentimes: 20000, percentage: null,
            countsUnjustifiedAbsent: false, createdAtUtc: Now, createdByUserId: 1);

    [Fact]
    public void Create_TeacherDefault_PerPresentStudent_Valid()
    {
        var policy = TeacherDefault();

        Assert.Equal(PayeeKind.Teacher, policy.PayeeKind);
        Assert.Equal(3, policy.TeacherId);
        Assert.Null(policy.EmployeeId);
        Assert.Null(policy.ClassGroupId);               // افتراضية على الأستاذ
        Assert.Equal(PayPolicyKind.PerPresentStudent, policy.Kind);
        Assert.Equal(20000, policy.RateCentimes);
        Assert.Null(policy.Percentage);
        Assert.False(policy.CountsUnjustifiedAbsent);   // الافتراضي: الغياب غير المبرر لا يُحتسب (D-114)
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void Create_TeacherOverride_PerHour_Valid()
    {
        var policy = PayPolicy.Create(PayeeKind.Teacher, 3, null, classGroupId: 9,
            PayPolicyKind.PerHour, 150000, null, true, Now, 1);

        Assert.Equal(9, policy.ClassGroupId);           // تجاوز لفوج محدد (D-113)
        Assert.True(policy.CountsUnjustifiedAbsent);
    }

    [Fact]
    public void Create_TeacherPercentage_Valid()
    {
        var policy = PayPolicy.Create(PayeeKind.Teacher, 3, null, null,
            PayPolicyKind.Percentage, 0, 60m, false, Now, 1);

        Assert.Equal(60m, policy.Percentage);
        Assert.Equal(0, policy.RateCentimes);           // القيمة في حقل النسبة — لا ثابتة
    }

    [Fact]
    public void Create_EmployeePerDay_Valid()
    {
        var policy = PayPolicy.Create(PayeeKind.Employee, null, 4, null,
            PayPolicyKind.PerDay, 100000, null, false, Now, 1);

        Assert.Equal(PayeeKind.Employee, policy.PayeeKind);
        Assert.Equal(PayPolicyKind.PerDay, policy.Kind);
    }

    [Fact]
    public void Create_EmployeePerMonth_Valid()
    {
        var policy = PayPolicy.Create(PayeeKind.Employee, null, 4, null,
            PayPolicyKind.PerMonth, 1500000, null, false, Now, 1);

        Assert.Equal(PayPolicyKind.PerMonth, policy.Kind);
    }

    [Theory]
    [InlineData(PayPolicyKind.PerDay)]
    [InlineData(PayPolicyKind.PerMonth)]
    public void Create_TeacherWithEmployeeKind_Throws(PayPolicyKind kind)
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, kind, 1000, null, false, Now, null));
    }

    [Theory]
    [InlineData(PayPolicyKind.PerPresentStudent)]
    [InlineData(PayPolicyKind.Percentage)]
    [InlineData(PayPolicyKind.PerHour)]
    public void Create_EmployeeWithTeacherKind_Throws(PayPolicyKind kind)
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Employee, null, 4, null, kind, 1000, kind == PayPolicyKind.Percentage ? 50m : null, false, Now, null));
    }

    [Fact]
    public void Create_TeacherMissingTeacherId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, null, null, null, PayPolicyKind.PerHour, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_TeacherWithEmployeeId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, employeeId: 4, null, PayPolicyKind.PerHour, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_EmployeeMissingEmployeeId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Employee, null, null, null, PayPolicyKind.PerDay, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_EmployeeWithTeacherId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Employee, 3, 4, null, PayPolicyKind.PerDay, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_EmployeeWithGroup_Throws()      // التجاوز بالفوج للأساتذة فقط (D-113)
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Employee, null, 4, classGroupId: 9, PayPolicyKind.PerDay, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_GroupIdZero_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, classGroupId: 0, PayPolicyKind.PerHour, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_PercentageKind_WithRate_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, PayPolicyKind.Percentage, 5000, 50m, false, Now, null));
    }

    [Fact]
    public void Create_PercentageKind_WithoutPercentage_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, PayPolicyKind.Percentage, 0, null, false, Now, null));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.01)]
    public void Create_PercentageOutOfBounds_Throws(double percentage)
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, PayPolicyKind.Percentage, 0, (decimal)percentage, false, Now, null));
    }

    [Fact]
    public void Create_FixedKind_WithPercentage_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, PayPolicyKind.PerHour, 1000, 50m, false, Now, null));
    }

    [Fact]
    public void Create_FixedKind_ZeroRate_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, PayPolicyKind.PerPresentStudent, 0, null, false, Now, null));
    }

    [Fact]
    public void Create_InvalidPayeeKind_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create((PayeeKind)9, 3, null, null, PayPolicyKind.PerHour, 1000, null, false, Now, null));
    }

    [Fact]
    public void Create_InvalidKind_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PayPolicy.Create(PayeeKind.Teacher, 3, null, null, (PayPolicyKind)9, 1000, null, false, Now, null));
    }

    [Fact]
    public void Update_ChangesKindValueFlag_AndKeepsIdentity()
    {
        var policy = TeacherDefault();
        var later = Now.AddHours(2);

        policy.Update(PayPolicyKind.PerHour, 150000, null, true, later, 2);

        Assert.Equal(PayPolicyKind.PerHour, policy.Kind);
        Assert.Equal(150000, policy.RateCentimes);
        Assert.True(policy.CountsUnjustifiedAbsent);
        Assert.Equal(3, policy.TeacherId);              // الهوية ثابتة (روح D-61)
        Assert.Null(policy.ClassGroupId);
        Assert.Equal(later, policy.UpdatedAtUtc);
        Assert.Equal(2, policy.UpdatedByUserId);
    }

    [Fact]
    public void Update_InvalidCombo_Throws_AndKeepsOldValues()
    {
        var policy = TeacherDefault();

        Assert.Throws<DomainException>(() =>
            policy.Update(PayPolicyKind.Percentage, 5000, 50m, false, Now, null));   // نسبة مع قيمة ثابتة

        Assert.Equal(PayPolicyKind.PerPresentStudent, policy.Kind);   // لم تتغير — التحقق قبل الإسناد
        Assert.Equal(20000, policy.RateCentimes);
    }

    [Fact]
    public void Deactivate_Twice_SecondIsNoOp()
    {
        var policy = TeacherDefault();
        var first = Now.AddHours(1);

        policy.Deactivate(first, 2);
        policy.Deactivate(first.AddHours(1), 3);       // معطّلة أصلاً — لا ختم ثانٍ

        Assert.False(policy.IsActive);
        Assert.Equal(first, policy.UpdatedAtUtc);
        Assert.Equal(2, policy.UpdatedByUserId);
    }

    [Fact]
    public void Activate_AfterDeactivate_Reactivates()
    {
        var policy = TeacherDefault();

        policy.Deactivate(Now, 1);
        policy.Activate(Now.AddHours(1), 2);

        Assert.True(policy.IsActive);
        Assert.Equal(2, policy.UpdatedByUserId);
    }
}