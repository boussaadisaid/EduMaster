using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Employees;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Payroll;
using EduMaster.Application.Teachers;
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