using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Enrollments;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using EduMaster.Domain.Students;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>
/// الترحيل الجماعي (6.2 — D-129): الـhandler الفردي الحقيقي يُستعمل لكل طالب (لا مزيّف له أبداً — تر-7) · الحقوق من السنة الهدف
/// دائماً والإعفاء لا يُنسخ (تر-4) · التكرار الودّي = تخطٍّ (إعادة ضغط آمنة روح D-87) · بلا خريطة = فشل مرئي بسطر اصطياد (D-121) ·
/// التحققات قبل أي قراءة · الخريطة الفاسدة تُرفض مقدماً قبل أي كتابة · الإلغاء يُرمى (D-64).
/// </summary>
public sealed class BulkRolloverHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);

    private const int SourceYearId = 1;   // 2025-2026 — حقوقها 25000
    private const int TargetYearId = 2;   // 2026-2027 — حقوقها 30000
    private const int SourceLevelId = 10;
    private const int TargetLevelId = 20;

    /// <summary>فراش الاختبار: مزيّفات المستودعات + الـhandler الفردي الحقيقي + الجماعي فوقه</summary>
    private sealed class TestBed
    {
        public FakeAnnualEnrollmentRepository Enrollments { get; } = new();
        public FakeStudentRepository Students { get; } = new();
        public FakePersonRepository Persons { get; } = new();
        public FakeAcademicYearRepository Years { get; } = new();
        public FakeLevelRepository Levels { get; } = new();
        public FakeStreamRepository Streams { get; } = new();
        public FakeChargeRepository Charges { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public BulkRolloverHandler Handler { get; }

        public TestBed()
        {
            var register = new RegisterAnnualEnrollmentHandler(
                Enrollments, Students, Persons, Years, Levels, Streams, Charges,
                new FakeClock(), new FakeCurrentUserService(), Uow,
                new EduMaster.Application.Billing.CreditConsumptionService(new FakePaymentRepository(), Charges),
                NullLogger<RegisterAnnualEnrollmentHandler>.Instance);
            Handler = new BulkRolloverHandler(register, Enrollments, Years, Levels, Streams,
                NullLogger<BulkRolloverHandler>.Instance);
        }
    }

    private static AcademicYear NewYear(int id, string name, int startYear, long fee, bool active = true) =>
        AcademicYear.Load(id, new YearName(name), new DateOnly(startYear, 9, 1), new DateOnly(startYear + 1, 6, 30),
            isCurrent: false, isActive: active, registrationFeeCentimes: fee,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static Domain.Academic.Level NewLevel(int id, string name, bool active = true) =>
        Domain.Academic.Level.Load(id, name, sortOrder: id, isActive: active,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static Person NewPerson(int id)
    {
        var person = Person.Create(new FirstName("أمين"), new LastName("بن يوسف"), null, null, null, null, null, null, null, null, Now, 1);
        person.SetId(id);
        return person;
    }

    private static Student NewStudent(int id, int personId)
    {
        var student = Student.Create(personId, null, StudentCategory.Regular, null, Now, 1);
        student.SetId(id);
        return student;
    }

    /// <summary>تسجيل نشط في سنة المصدر — fee=0 يجسّد المعفى (لبرهان «لا نسخ للإعفاء» تر-4)</summary>
    private static AnnualEnrollment SourceEnrollment(int studentId, int levelId, long fee = 25000) =>
        AnnualEnrollment.Create(studentId, SourceYearId, levelId, null, fee, null, Now, 1);

    private static TestBed BedWithYearsAndLevels()
    {
        var bed = new TestBed();
        bed.Years.ById[SourceYearId] = NewYear(SourceYearId, "2025-2026", 2025, 25000);
        bed.Years.ById[TargetYearId] = NewYear(TargetYearId, "2026-2027", 2026, 30000);
        bed.Levels.ById[SourceLevelId] = NewLevel(SourceLevelId, "الثالثة ثانوي");
        bed.Levels.ById[TargetLevelId] = NewLevel(TargetLevelId, "تخرج");
        return bed;
    }

    private static void AddStudent(TestBed bed, int studentId)
    {
        var personId = 100 + studentId;
        bed.Persons.ById[personId] = NewPerson(personId);
        bed.Students.ById[studentId] = NewStudent(studentId, personId);
    }

    private static BulkRolloverRequest OneMappingRequest(params int[] studentIds) =>
        new(SourceYearId, TargetYearId,
            new List<RolloverMappingInput> { new(SourceLevelId, null, TargetLevelId, null) },
            new List<int>(studentIds));

    [Fact]
    public async Task HappyPath_MixedOutcomes_SuccessSkippedFailed_AndFeeFromTargetYear()
    {
        var bed = BedWithYearsAndLevels();
        // 1: نجاح — معفى في المصدر (0) لكنه يأخذ حقوق الهدف 30000 (تر-4: الإعفاء لا يُنسخ)
        AddStudent(bed, 1);
        bed.Enrollments.ByActive[(1, SourceYearId)] = SourceEnrollment(1, SourceLevelId, fee: 0);
        // 2: تخطٍّ — له تسجيل نشط في الهدف أصلاً (إعادة ضغط آمنة روح D-87)
        AddStudent(bed, 2);
        bed.Enrollments.ByActive[(2, SourceYearId)] = SourceEnrollment(2, SourceLevelId);
        bed.Enrollments.ByActive[(2, TargetYearId)] =
            AnnualEnrollment.Create(2, TargetYearId, TargetLevelId, null, 30000, null, Now, 1);
        // 3: فشل — مستواه 99 بلا صف في الخريطة (مرئي بسببه — لا اختفاء صامت)
        AddStudent(bed, 3);
        bed.Levels.ById[99] = NewLevel(99, "مستوى بلا خريطة");
        bed.Enrollments.ByActive[(3, SourceYearId)] = SourceEnrollment(3, 99);
        // 4: تخطٍّ — بلا تسجيل نشط في المصدر (تغيّر بعد المعاينة)
        AddStudent(bed, 4);

        var result = await bed.Handler.ExecuteAsync(OneMappingRequest(1, 2, 3, 4));

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(1, report.SuccessCount);
        Assert.Equal(2, report.SkippedCount);
        Assert.Equal(1, report.FailedCount);

        Assert.Equal(RolloverOutcome.Success, report.Rows[0].Outcome);
        Assert.Null(report.Rows[0].Reason);

        Assert.Equal(RolloverOutcome.Skipped, report.Rows[1].Outcome);   // Conflict الفردي الودّي ← تخطٍّ
        Assert.NotNull(report.Rows[1].Reason);

        Assert.Equal(RolloverOutcome.Failed, report.Rows[2].Outcome);
        Assert.Contains("الخريطة", report.Rows[2].Reason!);

        Assert.Equal(RolloverOutcome.Skipped, report.Rows[3].Outcome);
        Assert.Contains("المصدر", report.Rows[3].Reason!);

        // تر-4: الناجي الوحيد أخذ حقوق الهدف كاملةً رغم إعفائه في المصدر
        var enrollment = Assert.Single(bed.Enrollments.Added);
        Assert.Equal(1, enrollment.StudentId);
        Assert.Equal(TargetYearId, enrollment.AcademicYearId);
        Assert.Equal(TargetLevelId, enrollment.LevelId);
        Assert.Equal(30000, enrollment.AgreedRegistrationFeeCentimes);

        // D-103: مستحق الحقوق تولّد ذرّياً في معاملة التسجيل — مرة واحدة لناجح واحد
        Assert.Single(bed.Charges.Added);
        Assert.Equal(1, bed.Uow.BeganCount);
        Assert.Equal(1, bed.Uow.CommittedCount);
        Assert.Equal(0, bed.Uow.RolledBackCount);
    }

    [Fact]
    public async Task SameSourceAndTarget_ValidationFailure_BeforeAnyRead()
    {
        var bed = new TestBed();

        var result = await bed.Handler.ExecuteAsync(
            new BulkRolloverRequest(SourceYearId, SourceYearId,
                new List<RolloverMappingInput> { new(SourceLevelId, null, TargetLevelId, null) },
                new List<int> { 1 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(0, bed.Years.GetByIdCallCount);   // قبل أي قراءة
    }

    [Fact]
    public async Task EmptyMappings_ValidationFailure_BeforeAnyRead()
    {
        var bed = new TestBed();

        var result = await bed.Handler.ExecuteAsync(
            new BulkRolloverRequest(SourceYearId, TargetYearId,
                new List<RolloverMappingInput>(), new List<int> { 1 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(0, bed.Years.GetByIdCallCount);
    }

    [Fact]
    public async Task EmptyStudents_ValidationFailure()
    {
        var bed = new TestBed();

        var result = await bed.Handler.ExecuteAsync(OneMappingRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(bed.Enrollments.Added);
    }

    [Fact]
    public async Task TargetYearInactive_BusinessRuleFailure_NothingWritten()
    {
        var bed = BedWithYearsAndLevels();
        bed.Years.ById[TargetYearId] = NewYear(TargetYearId, "2026-2027", 2026, 30000, active: false);

        var result = await bed.Handler.ExecuteAsync(OneMappingRequest(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Empty(bed.Enrollments.Added);
        Assert.Equal(0, bed.Uow.BeganCount);   // لا معاملة قبل اكتمال الحُراس
    }

    [Fact]
    public async Task DuplicateMappingSource_ValidationFailure_NothingWritten()
    {
        var bed = BedWithYearsAndLevels();
        var mappings = new List<RolloverMappingInput>
        {
            new(SourceLevelId, null, TargetLevelId, null),
            new(SourceLevelId, null, 30, null),   // نفس (المستوى + الشعبة) المصدر مرتين = غموض
        };

        var result = await bed.Handler.ExecuteAsync(
            new BulkRolloverRequest(SourceYearId, TargetYearId, mappings, new List<int> { 1 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(bed.Enrollments.Added);
    }

    [Fact]
    public async Task InactiveTargetLevel_BusinessRuleFailure_NothingWritten()
    {
        var bed = BedWithYearsAndLevels();
        bed.Levels.ById[TargetLevelId] = NewLevel(TargetLevelId, "تخرج", active: false);

        var result = await bed.Handler.ExecuteAsync(OneMappingRequest(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Contains("معطّل", result.ErrorMessage!);
        Assert.Empty(bed.Enrollments.Added);
    }

    [Fact]
    public async Task MappingStreamOutsideTargetLevel_ValidationFailure_NothingWritten()
    {
        var bed = BedWithYearsAndLevels();
        // مستوى الهدف بلا شعب في المزيّف ⇒ أي شعبة هدف تُرفض (حارس «الشعبة تتبع المستوى» نفسه — D-43)

        var result = await bed.Handler.ExecuteAsync(
            new BulkRolloverRequest(SourceYearId, TargetYearId,
                new List<RolloverMappingInput> { new(SourceLevelId, null, TargetLevelId, 555) },
                new List<int> { 1 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(bed.Enrollments.Added);
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64: الإلغاء ليس خطأً
    {
        var bed = new TestBed();
        bed.Years.ToThrow = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => bed.Handler.ExecuteAsync(OneMappingRequest(1)));
    }
}