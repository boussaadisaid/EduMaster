using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Employees;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Payroll;
using EduMaster.Application.Students;
using EduMaster.Application.Teachers;
using EduMaster.Domain.Treasury;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EduMaster.Application.Tests.Fakes;

/// <summary>ساعة مزيّفة — الوقت يُمرَّر ولا يُقرأ (D-20)</summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

public sealed class FakeCurrentUserService : ICurrentUserService
{
    public int? UserAccountId { get; set; } = 1;
    public string? Username { get; set; } = "admin";
}

/// <summary>وحدة عمل مزيّفة تعدّ خطواتها — برهان «Commit في النجاح، لا شيء قبل الحُراس»</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int BeganCount { get; private set; }
    public int CommittedCount { get; private set; }
    public int RolledBackCount { get; private set; }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) { BeganCount++; return Task.CompletedTask; }
    public Task CommitAsync(CancellationToken cancellationToken = default) { CommittedCount++; return Task.CompletedTask; }
    public Task RollbackAsync(CancellationToken cancellationToken = default) { RolledBackCount++; return Task.CompletedTask; }
}

/// <summary>مزيّف المشتريات — يلتقط ويمنح معرفاً (SetId متاح عبر InternalsVisibleTo) لأن مستحق الحزمة يحتاجه (D-103)</summary>
/// <summary>مزيّف الحسابات المالية — يعيد حساباً فعالاً افتراضياً لتهيئة اختبارات القبض/الصرف بعد ربطها بالخزينة.</summary>
public sealed class FakeTreasuryAccountRepository : ITreasuryAccountRepository
{
    public TreasuryAccount? AccountToReturn { get; set; }

    public FakeTreasuryAccountRepository()
    {
        AccountToReturn = TreasuryAccount.Load(
            id: 1, name: "الخزينة الرئيسية", isActive: true, openingBalanceCentimes: 0,
            createdAtUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);
    }

    public Task AddAsync(TreasuryAccount account, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateAsync(TreasuryAccount account, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<TreasuryAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(id == 1 ? AccountToReturn : null);

    public Task<IReadOnlyList<TreasuryAccount>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

public sealed class FakeGroupSessionPurchaseRepository : IGroupSessionPurchaseRepository
{
    public List<Domain.Scheduling.GroupSessionPurchase> Captured { get; } = new();

    public Task AddAsync(Domain.Scheduling.GroupSessionPurchase purchase, CancellationToken cancellationToken = default)
    {
        purchase.SetId(1);   // محاكاة OUTPUT INSERTED.Id
        Captured.Add(purchase);
        return Task.CompletedTask;
    }
}

/// <summary>مزيّف المستحقات — يلتقط الإضافات والتحديثات + ChargesById لمسارات القبض + OpenToReturn للمفتوحة</summary>
public sealed class FakeChargeRepository : IChargeRepository
{
    public List<Domain.Billing.Charge> Added { get; } = new();
    public List<Domain.Billing.Charge> Updated { get; } = new();
    public Domain.Billing.Charge? EntityToReturn { get; set; }
    public IReadOnlyDictionary<int, Domain.Billing.Charge> ChargesById { get; set; } = new Dictionary<int, Domain.Billing.Charge>();
    public IReadOnlyList<OpenChargeItem> OpenToReturn { get; set; } = new List<OpenChargeItem>();
    public bool HasAnyValue { get; set; }
    public long AllocatedForChargeValue { get; set; }   // 6.6-ع-3

    public Task AddAsync(Domain.Billing.Charge charge, CancellationToken cancellationToken = default)
    {
        Added.Add(charge);
        return Task.CompletedTask;
    }

    public Task<Domain.Billing.Charge?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // ChargesById أولاً (مسارات القبض) — ثم EntityToReturn (مسارات التسوية) توافقاً مع الاستعمال القديم
        if (ChargesById.TryGetValue(id, out var charge))
            return Task.FromResult<Domain.Billing.Charge?>(charge);
        return Task.FromResult(EntityToReturn);
    }

    public Task<long> GetAllocatedForChargeAsync(int chargeId, CancellationToken cancellationToken = default)
        => Task.FromResult(AllocatedForChargeValue);

    public Task UpdateAsync(Domain.Billing.Charge charge, CancellationToken cancellationToken = default)
    {
        Updated.Add(charge);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<StudentChargeItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<OpenChargeItem>> GetOpenForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult(OpenToReturn.AsEnumerable());

    public Task<IEnumerable<DebtorItem>> GetDebtorsAsync(string? searchTerm, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();   // قراءة 4.3 — لا تُستدعى في اختبارات الكتابة

    public Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasAnyValue);
}

/// <summary>مزيّف المدفوعات — يلتقط الإيصالات والتخصيصات + رقم إيصال مضبوط + زائدة مضبوطة (تحرس الصرف)</summary>
public sealed class FakePaymentRepository : IPaymentRepository
{
    public int NextReceiptNo { get; set; } = 1;
    public List<Domain.Billing.Payment> Payments { get; } = new();
    public List<Domain.Billing.PaymentAllocation> Allocations { get; } = new();
    public long UnallocatedValue { get; set; }
    public bool HasAnyValue { get; set; }

    public Task AddAsync(Domain.Billing.Payment payment, CancellationToken cancellationToken = default)
    {
        payment.SetId(1);   // محاكاة OUTPUT INSERTED.Id — التخصيصات تتبعه
        Payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task AddAllocationAsync(Domain.Billing.PaymentAllocation allocation, CancellationToken cancellationToken = default)
    {
        Allocations.Add(allocation);
        return Task.CompletedTask;
    }

    public Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(NextReceiptNo);

    public Task<long> GetUnallocatedForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult(UnallocatedValue);

    public List<UnallocatedReceiptRaw> UnallocatedReceipts { get; } = new();

    public Task<IReadOnlyList<UnallocatedReceiptRaw>> GetUnallocatedReceiptsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UnallocatedReceiptRaw>>(UnallocatedReceipts);

    public ReceiptReversalInfoRaw? ReversalInfoToReturn { get; set; }
    public Exception? ToThrowOnReversalRead { get; set; }
    public List<int> DeletedAllocationsForPayments { get; } = new();
    public List<int> DeletedAllocationsForCharges { get; } = new();

    public Task<ReceiptReversalInfoRaw?> GetReceiptReversalInfoAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        if (ToThrowOnReversalRead is not null) throw ToThrowOnReversalRead;
        return Task.FromResult(ReversalInfoToReturn);
    }

    public Task DeleteAllocationsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        DeletedAllocationsForPayments.Add(paymentId);
        return Task.CompletedTask;
    }

    public Task DeleteAllocationsForChargeAsync(int chargeId, CancellationToken cancellationToken = default)
    {
        DeletedAllocationsForCharges.Add(chargeId);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<PaymentListItem>> GetForPeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();   // قراءة 4.3 — لا تُستدعى في اختبارات الكتابة

    public Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasAnyValue);
}

/// <summary>مستودع تسجيلات الأفواج مزيّف — GetByIdAsync وحده يعمل (يعيد الكيان المزروع)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakeClassGroupEnrollmentRepository : IClassGroupEnrollmentRepository
{
    public Domain.Enrollments.ClassGroupEnrollment? EntityToReturn { get; set; }

    public Task<Domain.Enrollments.ClassGroupEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(EntityToReturn);

    public Task AddAsync(Domain.Enrollments.ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task UpdateAsync(Domain.Enrollments.ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> AnyActiveForStudentInGroupAsync(int classGroupId, int studentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<int> CountActiveInGroupAsync(int classGroupId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IEnumerable<ClassGroupEnrollmentListItem>> GetForGroupAsync(int classGroupId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IEnumerable<StudentGroupEnrollmentItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<Domain.Enrollments.ClassGroupEnrollment>> GetActiveByAnnualEnrollmentIdAsync(int annualEnrollmentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IEnumerable<ClassGroupListItem>> GetTransferTargetsAsync(int groupEnrollmentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IEnumerable<ClassGroupListItem>> GetEnrollableGroupsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

/// <summary>مزيّف الموظفين (F5) — GetByIdAsync يعيد المزروع · يلتقط الإضافات والتحديثات والحذف المنطقي</summary>
public sealed class FakeEmployeeRepository : IEmployeeRepository
{
    public Domain.Employees.Employee? EntityToReturn { get; set; }
    public bool AnyActiveValue { get; set; }
    public bool HasOperationalDataValue { get; set; }
    public List<Domain.Employees.Employee> Added { get; } = new();
    public List<Domain.Employees.Employee> Updated { get; } = new();
    public int? SoftDeletedId { get; private set; }

    public Task AddAsync(Domain.Employees.Employee employee, CancellationToken cancellationToken = default)
    {
        employee.SetId(1);   // محاكاة OUTPUT INSERTED.Id
        Added.Add(employee);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Domain.Employees.Employee employee, CancellationToken cancellationToken = default)
    {
        Updated.Add(employee);
        return Task.CompletedTask;
    }

    public Task<Domain.Employees.Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(EntityToReturn);

    public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default)
        => Task.FromResult(AnyActiveValue);

    public Task<IEnumerable<EmployeeListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();   // قراءة — لا تُستدعى في اختبارات الكتابة

    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(HasOperationalDataValue);

    public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        SoftDeletedId = id;
        return Task.CompletedTask;
    }
}

/// <summary>مزيّف سجل أيام العمل (F5) — يلتقط الأيام والمحذوفات · DeleteResult مضبوط · ItemsToReturn لفحص الفرادة الودّي</summary>
public sealed class FakeEmployeeWorkLogRepository : IEmployeeWorkLogRepository
{
    public List<Domain.Payroll.WorkLogEntry> Added { get; } = new();
    public List<int> DeletedIds { get; } = new();
    public IReadOnlyList<WorkLogItem> ItemsToReturn { get; set; } = new List<WorkLogItem>();
    public int DeleteResult { get; set; } = 1;

    public Task AddAsync(Domain.Payroll.WorkLogEntry entry, CancellationToken cancellationToken = default)
    {
        entry.SetId(1);   // محاكاة OUTPUT INSERTED.Id
        Added.Add(entry);
        return Task.CompletedTask;
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
        return Task.FromResult(DeleteResult);
    }

    public Task<IReadOnlyList<WorkLogItem>> GetForEmployeeAsync(int employeeId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        => Task.FromResult(ItemsToReturn);
}

/// <summary>مزيّف سياسات الأجر (F5) — EntityToReturn للجلب · ActiveToReturn لفحص فرادة النطاق الودّي · يلتقط الإضافات والتحديثات</summary>
public sealed class FakePayPolicyRepository : IPayPolicyRepository
{
    public Domain.Payroll.PayPolicy? EntityToReturn { get; set; }
    public Domain.Payroll.PayPolicy? ActiveToReturn { get; set; }
    public List<Domain.Payroll.PayPolicy> Added { get; } = new();
    public List<Domain.Payroll.PayPolicy> Updated { get; } = new();

    public Task<Domain.Payroll.PayPolicy?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(EntityToReturn);

    public Task<Domain.Payroll.PayPolicy?> GetActiveDefaultForTeacherAsync(int teacherId, CancellationToken cancellationToken = default)
        => Task.FromResult(ActiveToReturn);

    public Task<Domain.Payroll.PayPolicy?> GetActiveOverrideAsync(int teacherId, int classGroupId, CancellationToken cancellationToken = default)
        => Task.FromResult(ActiveToReturn);

    public Task<Domain.Payroll.PayPolicy?> GetActiveForEmployeeAsync(int employeeId, CancellationToken cancellationToken = default)
        => Task.FromResult(ActiveToReturn);

    public Task<IReadOnlyList<PayPolicyItem>> ListAsync(Domain.Payroll.PayeeKind? payeeKind, int? payeeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();   // قراءة — لا تُستدعى في اختبارات الكتابة

    public Task AddAsync(Domain.Payroll.PayPolicy policy, CancellationToken cancellationToken = default)
    {
        policy.SetId(1);   // محاكاة OUTPUT INSERTED.Id
        Added.Add(policy);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Domain.Payroll.PayPolicy policy, CancellationToken cancellationToken = default)
    {
        Updated.Add(policy);
        return Task.CompletedTask;
    }
}

/// <summary>مزيّف الأساتذة — GetByIdAsync وحده يعمل (حارس وجود المستفيد في سياسات F5)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakeTeacherRepository : ITeacherRepository
{
    public Domain.Teachers.Teacher? EntityToReturn { get; set; }

    public Task<Domain.Teachers.Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(EntityToReturn);

    public Task AddAsync(Domain.Teachers.Teacher teacher, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task UpdateAsync(Domain.Teachers.Teacher teacher, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IEnumerable<TeacherListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

/// <summary>مزيّف الأفواج — GetByIdAsync وحده يعمل (حارس وجود فوج التجاوز في سياسات F5)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakeClassGroupRepository : IClassGroupRepository
{
    public Domain.ClassGroups.ClassGroup? EntityToReturn { get; set; }

    public Task<Domain.ClassGroups.ClassGroup?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(EntityToReturn);

    public Task AddAsync(Domain.ClassGroups.ClassGroup classGroup, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task UpdateAsync(Domain.ClassGroups.ClassGroup classGroup, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> AnyWithNameInYearAsync(int academicYearId, string name, int? excludeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IEnumerable<ClassGroupListItem>> SearchAsync(int? academicYearId, string? normalizedTerm, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<int>> GetStreamIdsAsync(int classGroupId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task ReplaceStreamsAsync(int classGroupId, int levelId, IReadOnlyList<int> streamIds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

/// <summary>مزيّف التسجيلات السنوية (F6 — 6.2) — ByActive[(طالب, سنة)] للنشطة · يلتقط الإضافات بمعرفات متتالية · قراءة مرشحي الترحيل SQL لا تُستدعى في اختبارات الـHandler</summary>
public sealed class FakeAnnualEnrollmentRepository : IAnnualEnrollmentRepository
{
    private int _nextId = 1;

    public Dictionary<(int StudentId, int YearId), Domain.Enrollments.AnnualEnrollment> ByActive { get; } = new();
    public List<Domain.Enrollments.AnnualEnrollment> Added { get; } = new();

    public Task AddAsync(Domain.Enrollments.AnnualEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        enrollment.SetId(_nextId++);   // محاكاة OUTPUT INSERTED.Id — متتالية لكل صف جماعي
        Added.Add(enrollment);
        return Task.CompletedTask;
    }

    public Task<bool> AnyActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default)
        => Task.FromResult(ByActive.ContainsKey((studentId, academicYearId)));

    public Task<Domain.Enrollments.AnnualEnrollment?> GetActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default)
        => Task.FromResult(ByActive.TryGetValue((studentId, academicYearId), out var enrollment) ? enrollment : null);

    public Task UpdateAsync(Domain.Enrollments.AnnualEnrollment enrollment, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Domain.Enrollments.AnnualEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IEnumerable<AnnualEnrollmentListItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> HasActiveGroupEnrollmentsAsync(int annualEnrollmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<RolloverCandidateItem>> GetRolloverCandidatesAsync(int sourceYearId, int targetYearId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();   // قراءة معاينة 6.2 — SQL لا يُختبر بالمزيّفات
}

/// <summary>مزيّف ملفات الطلاب (F6 — 6.2) — ById للجلب (حارس الوجود في التسجيل)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakeStudentRepository : IStudentRepository
{
    public Dictionary<int, Domain.Students.Student> ById { get; } = new();

    public Task<Domain.Students.Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(ById.TryGetValue(id, out var student) ? student : null);

    public Task AddAsync(Domain.Students.Student student, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task UpdateAsync(Domain.Students.Student student, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IEnumerable<StudentListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

/// <summary>مزيّف الأشخاص (F6 — 6.2) — ById للجلب (حارس «الشخص فعّال» في التسجيل)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakePersonRepository : IPersonRepository
{
    public Dictionary<int, Domain.People.Person> ById { get; } = new();

    public Task<Domain.People.Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(ById.TryGetValue(id, out var person) ? person : null);

    public Task AddAsync(Domain.People.Person person, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task UpdateAsync(Domain.People.Person person, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IEnumerable<Domain.People.Person>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Application.People.PersonDuplicateRaw? MatchToReturn { get; set; }
    public string? LastNormalizedNameReceived { get; private set; }
    public Exception? ToThrowOnDuplicateCheck { get; set; }

    public Task<Application.People.PersonDuplicateRaw?> GetByNormalizedFullNameAsync(string normalizedFullName, CancellationToken cancellationToken = default)
    {
        if (ToThrowOnDuplicateCheck is not null) throw ToThrowOnDuplicateCheck;
        LastNormalizedNameReceived = normalizedFullName;
        return Task.FromResult(MatchToReturn);
    }
}

/// <summary>مزيّف السنوات الدراسية (F6 — 6.2) — ById للجلب بعدّاد استدعاءات (برهان «التحقق قبل أي قراءة») + ToThrow لمسارَي الإلغاء وغير المتوقع</summary>
public sealed class FakeAcademicYearRepository : IAcademicYearRepository
{
    public Dictionary<int, Domain.AcademicYears.AcademicYear> ById { get; } = new();
    public Domain.AcademicYears.AcademicYear? CurrentToReturn { get; set; }
    public int GetByIdCallCount { get; private set; }
    public int GetCurrentCallCount { get; private set; }
    public Exception? ToThrow { get; set; }

    public Task<Domain.AcademicYears.AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        if (ToThrow is not null) throw ToThrow;
        return Task.FromResult(ById.TryGetValue(id, out var year) ? year : null);
    }

    public Task AddAsync(Domain.AcademicYears.AcademicYear academicYear, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task UpdateAsync(Domain.AcademicYears.AcademicYear academicYear, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Domain.AcademicYears.AcademicYear?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default)
    {
        GetCurrentCallCount++;
        if (ToThrow is not null) throw ToThrow;
        return Task.FromResult(CurrentToReturn);
    }
    public Task<IEnumerable<Domain.AcademicYears.AcademicYear>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> AnyWithNameAsync(string name, int excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> AnyOverlappingAsync(DateOnly startDate, DateOnly endDate, int excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

/// <summary>مزيّف المستويات (F6 — 6.2) — ById للجلب (تحقق الخريطة المقدَّم)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakeLevelRepository : ILevelRepository
{
    public Dictionary<int, Domain.Academic.Level> ById { get; } = new();

    public Task<Domain.Academic.Level?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(ById.TryGetValue(id, out var level) ? level : null);

    public Task AddAsync(Domain.Academic.Level level, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task UpdateAsync(Domain.Academic.Level level, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Domain.Academic.Level>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

/// <summary>مزيّف الشعب (F6 — 6.2) — ByLevelId لشعب المستوى الهدف (تحقق الخريطة المقدَّم)، والباقي لا يُستدعى في المختبَر</summary>
public sealed class FakeStreamRepository : IStreamRepository
{
    public Dictionary<int, IReadOnlyList<Domain.Academic.Stream>> ByLevelId { get; } = new();

    public Task<IReadOnlyList<Domain.Academic.Stream>> GetByLevelIdAsync(int levelId, CancellationToken cancellationToken = default)
        => Task.FromResult(ByLevelId.TryGetValue(levelId, out var streams) ? streams : new List<Domain.Academic.Stream>());

    public Task AddAsync(Domain.Academic.Stream stream, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task UpdateAsync(Domain.Academic.Stream stream, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Domain.Academic.Stream?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> AnyWithNameInLevelAsync(int levelId, string name, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}