using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Reports;
using EduMaster.Application.Students;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Billing;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Students;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>كشف حساب طالب (6.1 — D-127/ت-6): طالب مفقود ← NotFound قبل أي قراءة أخرى · وصف التخصيص يُركَّب من قائمة المستحقات نفسها (تصويب ت-أ) · الصرف بلا تخصيص أبداً · الإلغاء يُرمى (D-64) · غير المتوقع عربي نظيف (D-24)</summary>
public sealed class GetStudentStatementHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);

    private sealed class StudentRepoFake : IStudentRepository
    {
        public Student? EntityToReturn { get; set; }
        public Exception? ToThrow { get; set; }

        public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(EntityToReturn);
        }

        public Task AddAsync(Student student, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Student student, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<StudentListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        // 6.3-ج: عضو جديد على العقد — غير مستعمل في اختبارات الكشف

    }

    private sealed class ChargeRepoFake : IChargeRepository
    {
        public IReadOnlyList<StudentChargeItem> ForStudentToReturn { get; set; } = new List<StudentChargeItem>();
        public bool Called { get; private set; }

        public Task<IEnumerable<StudentChargeItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(ForStudentToReturn.AsEnumerable());
        }

        public Task AddAsync(Charge charge, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Charge?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Charge charge, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<OpenChargeItem>> GetOpenForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<DebtorItem>> GetDebtorsAsync(string? searchTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<long> GetAllocatedForChargeAsync(int chargeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class AcademicYearRepoFake : IAcademicYearRepository
    {
        public Dictionary<int, AcademicYear> Items { get; } = new();
        public Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            Items.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }
        public Task<AcademicYear?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(AcademicYear academicYear, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(AcademicYear academicYear, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<AcademicYear>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyWithNameAsync(string name, int excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyOverlappingAsync(DateOnly startDate, DateOnly endDate, int excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class PaymentRepoFake : IPaymentRepository
    {
        public long UnallocatedValue { get; set; }
        public bool Called { get; private set; }

        public Task<long> GetUnallocatedForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(UnallocatedValue);
        }

        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAllocationAsync(PaymentAllocation allocation, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<UnallocatedReceiptRaw>> GetUnallocatedReceiptsForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReceiptReversalInfoRaw?> GetReceiptReversalInfoAsync(int paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAllocationsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAllocationsForChargeAsync(int chargeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<PaymentListItem>> GetForPeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class ReportRepoFake : IReportRepository
    {
        public StudentPaymentsRead ReadToReturn { get; set; } = new(new List<StudentPaymentRaw>(), new List<StudentPaymentAllocationRaw>());
        public Exception? ToThrow { get; set; }
        public bool Called { get; private set; }

        public Task<StudentPaymentsRead> GetPaymentsWithAllocationsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        {
            Called = true;
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(ReadToReturn);
        }

        public Task<ReceiptPrintRead?> GetReceiptForPrintAsync(int paymentId, CancellationToken cancellationToken = default)
    => throw new NotImplementedException();

        // 6.4-أ: عضوا العقد الجديدان — غير مستعملين في اختبارات الكشف
        public Task<IReadOnlyList<AttendanceMarkRaw>> GetAttendanceMarksForPeriodAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<EnrollmentBalanceRaw>> GetActiveEnrollmentBalancesAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private static Student AnyStudent() => Student.Create(5, null, StudentCategory.Regular, null, Now, 1);

    private static (GetStudentStatementHandler handler, StudentRepoFake students, ChargeRepoFake charges, PaymentRepoFake payments, ReportRepoFake reports, AcademicYearRepoFake years) Build(
        Student? student = null,
        IReadOnlyList<StudentChargeItem>? charges = null,
        StudentPaymentsRead? read = null,
        long credit = 0,
        AcademicYearRepoFake? years = null)
    {
        var students = new StudentRepoFake { EntityToReturn = student };
        var chargesRepo = new ChargeRepoFake { ForStudentToReturn = charges ?? new List<StudentChargeItem>() };
        var payments = new PaymentRepoFake { UnallocatedValue = credit };
        var reports = new ReportRepoFake { ReadToReturn = read ?? new(new List<StudentPaymentRaw>(), new List<StudentPaymentAllocationRaw>()) };
        years ??= new AcademicYearRepoFake();
        var handler = new GetStudentStatementHandler(students, chargesRepo, payments, reports, years,
            NullLogger<GetStudentStatementHandler>.Instance);
        return (handler, students, chargesRepo, payments, reports, years);
    }

    [Fact]
    public async Task StudentNotFound_NotFoundFailure_NothingBeyondExistenceCalled()
    {
        var (handler, _, charges, payments, reports, _) = Build(student: null);

        var result = await handler.ExecuteAsync(2);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.False(charges.Called);    // الوجود أولاً — لا قراءة مالية لطيفٍ مفقود
        Assert.False(reports.Called);
        Assert.False(payments.Called);
    }

    [Fact]
    public async Task Success_ComposesAllocationDescriptions_FromChargesList()   // برهان تصويب ت-أ
    {
        var charges = new List<StudentChargeItem>
        {
            new(7, 2, ChargeKind.RegistrationFee, "حقوق تسجيل 2025-2026", 200000, 200000, ChargeStatus.Active, null, Now, 50000),
            new(9, 2, ChargeKind.SessionBundle, "حزمة حصص رياضيات ×4", 100000, 100000, ChargeStatus.Cancelled, "إلغاء موثق", Now, 0),
        };
        var read = new StudentPaymentsRead(
            new List<StudentPaymentRaw>
            {
                new(1, 101, PaymentKind.Receipt, "الولي محمد", 80000, new DateTime(2026, 8, 24), null, 30000),
                new(2, 102, PaymentKind.Refund, null, 20000, new DateTime(2026, 8, 25), "استرجاع موثق", 0),
            },
            new List<StudentPaymentAllocationRaw> { new(1, 7, 30000) });

        var (handler, _, _, _, _, _) = Build(student: AnyStudent(), charges: charges, read: read, credit: 45000);

        var result = await handler.ExecuteAsync(2);

        Assert.True(result.IsSuccess);
        var statement = result.Value!;

        var receipt = statement.Payments[0];
        var allocation = Assert.Single(receipt.Allocations);
        Assert.Equal(7, allocation.ChargeId);
        Assert.Equal("حقوق تسجيل 2025-2026", allocation.SourceDescription);   // الوصف من قائمة المستحقات — لا نص من المستودع
        Assert.Equal(30000, allocation.AmountCentimes);

        var refund = statement.Payments[1];
        Assert.Empty(refund.Allocations);          // الصرف لا يُخصَّص أبداً
        Assert.Equal(0, refund.UnallocatedCentimes);

        Assert.Equal(150000, statement.BalanceCentimes);   // الفعّال فقط: 200000 − 50000 (الملغى لا يُحسب)
        Assert.Equal(80000, statement.ReceiptsTotalCentimes);
        Assert.Equal(20000, statement.RefundsTotalCentimes);
        Assert.Equal(45000, statement.CreditCentimes);     // الزائدة من مصدرها القائم (D-107)
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64
    {
        var (handler, students, _, _, _, _) = Build(student: AnyStudent());
        students.ToThrow = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.ExecuteAsync(2));
    }

    [Fact]
    public async Task UnexpectedException_ArabicFailure()   // D-24
    {
        var (handler, _, _, _, reports, _) = Build(student: AnyStudent());
        reports.ToThrow = new InvalidOperationException("raw boom");

        var result = await handler.ExecuteAsync(2);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);
    }



    [Fact]
    public async Task AcademicYearScope_KeepsOnlyTargetCharges_AndCountsOnlyAllocationToTarget()
    {
        var currentYear = AcademicYear.Load(20, new YearName("2026-2027"), new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31), true, true, 0, Now, 1, null, null);
        var previousYear = AcademicYear.Load(19, new YearName("2025-2026"), new DateOnly(2025, 9, 1), new DateOnly(2026, 8, 31), false, true, 0, Now, 1, null, null);
        var years = new AcademicYearRepoFake();
        years.Items[currentYear.Id] = currentYear;
        years.Items[previousYear.Id] = previousYear;

        var charges = new List<StudentChargeItem>
        {
            new(11, 5, ChargeKind.RegistrationFee, "حقوق تسجيل 2025-2026", 50000, 50000, ChargeStatus.Active, null, Now, 20000, previousYear.Id, previousYear.Name.Value),
            new(12, 5, ChargeKind.RegistrationFee, "حقوق تسجيل 2026-2027", 70000, 70000, ChargeStatus.Active, null, Now, 30000, currentYear.Id, currentYear.Name.Value),
        };

        var read = new StudentPaymentsRead(
            new List<StudentPaymentRaw>
            {
                new(1, 200, PaymentKind.Receipt, "الولي", 60000, Now, null, 50000),
                new(2, 201, PaymentKind.Refund, null, 10000, Now, "استرجاع", 0),
            },
            new List<StudentPaymentAllocationRaw>
            {
                new(1, 11, 20000),
                new(1, 12, 30000),
            });

        var (handler, _, _, _, _, _) = Build(student: AnyStudent(), charges: charges, read: read, credit: 15000, years: years);

        var result = await handler.ExecuteForAcademicYearAsync(5, currentYear.Id);

        Assert.True(result.IsSuccess);
        var statement = result.Value!;
        var charge = Assert.Single(statement.Charges);
        Assert.Equal(currentYear.Id, charge.AcademicYearId);
        Assert.Equal(40000, statement.BalanceCentimes);
        var payment = Assert.Single(statement.Payments);
        Assert.Equal(30000, payment.AllocatedToSelectedAcademicYearCentimes);
        Assert.Single(payment.Allocations);
        Assert.Equal(12, payment.Allocations[0].ChargeId);
        Assert.Equal(30000, statement.ReceiptsTotalCentimes);
        Assert.Equal(15000, statement.CreditCentimes);
        Assert.Equal(0, statement.RefundsTotalCentimes);
        Assert.True(statement.IsAcademicYearScoped);
        Assert.Equal(currentYear.Id, statement.AcademicYearId);
        Assert.Equal(currentYear.Name.Value, statement.AcademicYearName);
    }

    [Fact]
    public async Task FullHistory_RemainsUnchanged_WhenUsingOriginalExecuteAsync()
    {
        var previousYear = AcademicYear.Load(19, new YearName("2025-2026"), new DateOnly(2025, 9, 1), new DateOnly(2026, 8, 31), false, true, 0, Now, 1, null, null);
        var years = new AcademicYearRepoFake();
        years.Items[previousYear.Id] = previousYear;

        var charges = new List<StudentChargeItem>
        {
            new(11, 5, ChargeKind.RegistrationFee, "حقوق تسجيل 2025-2026", 50000, 50000, ChargeStatus.Active, null, Now, 20000, previousYear.Id, previousYear.Name.Value),
        };

        var read = new StudentPaymentsRead(
            new List<StudentPaymentRaw> { new(1, 200, PaymentKind.Receipt, "الولي", 60000, Now, null, 20000) },
            new List<StudentPaymentAllocationRaw> { new(1, 11, 20000) });

        var (handler, _, _, _, _, _) = Build(student: AnyStudent(), charges: charges, read: read, credit: 40000, years: years);
        var result = await handler.ExecuteAsync(5);

        Assert.True(result.IsSuccess);
        var statement = result.Value!;
        Assert.False(statement.IsAcademicYearScoped);
        Assert.Single(statement.Charges);
        Assert.Single(statement.Payments);
        Assert.Equal(60000, statement.ReceiptsTotalCentimes);
        Assert.Equal(40000, statement.CreditCentimes);
    }

}