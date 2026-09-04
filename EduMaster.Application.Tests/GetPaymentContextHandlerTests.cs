using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>حارس سياق القبض: السنة الحالية افتراضية، السنوات الأخرى تبقى قابلة للعرض صراحةً، والزائدة الدائنة عالمية.</summary>
public sealed class GetPaymentContextHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    private static AcademicYear Year(int id, string name, bool isCurrent)
        => AcademicYear.Load(
            id, new YearName(name),
            new DateOnly(int.Parse(name[..4]), 9, 1),
            new DateOnly(int.Parse(name[5..]), 8, 31),
            isCurrent, true, 0, Now, 1, null, null);

    private static OpenChargeItem Charge(int id, long amount, string yearName, int yearId, int daysAgo)
        => new(id, ChargeKind.SessionBundle, $"حزمة 4 حصص — الفوج {id}", amount, 0, Now.AddDays(-daysAgo), yearId, yearName);

    [Fact]
    public async Task SplitsCurrentAndOtherCharges_AndKeepsCreditGlobal()
    {
        var currentYear = Year(2, "2026-2027", true);
        var previousYear = Year(1, "2025-2026", false);

        var charges = new FakeChargeRepository
        {
            OpenToReturn = new List<OpenChargeItem>
            {
                Charge(11, 70000, "2025-2026", previousYear.Id, 20),
                Charge(12, 50000, "2026-2027", currentYear.Id, 10)
            }
        };
        var payments = new FakePaymentRepository { UnallocatedValue = 25000 };
        var years = new FakeAcademicYearRepository { CurrentToReturn = currentYear };
        var students = new FakeStudentRepository();

        var handler = new GetPaymentContextHandler(
            charges, payments, students, years,
            NullLogger<GetPaymentContextHandler>.Instance);

        var result = await handler.ExecuteAsync(2);

        Assert.True(result.IsSuccess);
        var context = result.Value!;
        Assert.Single(context.CurrentYearOpenCharges);
        Assert.Equal(currentYear.Id, context.CurrentYearOpenCharges[0].AcademicYearId);
        Assert.Single(context.OtherYearsOpenCharges);
        Assert.Equal(previousYear.Id, context.OtherYearsOpenCharges[0].AcademicYearId);
        Assert.Equal(25000, context.UnallocatedCentimes);
        Assert.Equal(currentYear.Id, context.CurrentAcademicYearId);
        Assert.Equal("2026-2027", context.CurrentAcademicYearName);
    }

    [Fact]
    public async Task FutureYearCharge_IsOtherYear_NotCurrentYear()
    {
        var currentYear = Year(2, "2026-2027", true);
        var futureYear = Year(3, "2027-2028", false);

        var charges = new FakeChargeRepository
        {
            OpenToReturn = new List<OpenChargeItem>
            {
                Charge(13, 60000, "2027-2028", futureYear.Id, 1)
            }
        };
        var payments = new FakePaymentRepository();
        var years = new FakeAcademicYearRepository { CurrentToReturn = currentYear };
        var students = new FakeStudentRepository();

        var handler = new GetPaymentContextHandler(
            charges, payments, students, years,
            NullLogger<GetPaymentContextHandler>.Instance);

        var result = await handler.ExecuteAsync(2);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.CurrentYearOpenCharges);
        Assert.Single(result.Value.OtherYearsOpenCharges);
        Assert.Equal(futureYear.Id, result.Value.OtherYearsOpenCharges[0].AcademicYearId);
    }

    [Fact]
    public async Task MissingCurrentAcademicYear_ReturnsBusinessRuleBeforePaymentReads()
    {
        var charges = new FakeChargeRepository();
        var payments = new FakePaymentRepository();
        var years = new FakeAcademicYearRepository();
        var students = new FakeStudentRepository();

        var handler = new GetPaymentContextHandler(
            charges, payments, students, years,
            NullLogger<GetPaymentContextHandler>.Instance);

        var result = await handler.ExecuteAsync(2);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Equal(1, years.GetCurrentCallCount);
    }
}
