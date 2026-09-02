using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Employees;
using EduMaster.Application.Payroll;
using EduMaster.Application.Teachers;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Employees;
using EduMaster.Domain.Payroll;
using EduMaster.Domain.Teachers;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>
/// تسجيل الصرف (D-125): رقم إيصال متتابع داخل المعاملة (مرآة D-105) · حارس السلفة (تجاوز الرصيد الجاري ⇒ ملاحظة توثيقية) ·
/// القيد السالب (تصحيح) يفرض ملاحظته الكيانُ · لا كتابة قبل الحُراس (Commit في النجاح فقط).
/// مزيّفات هذه الدفعة داخلية بالملف — بلا تماس مع TestFakes المشتركة (لا مخاطرة تكرار عبر الزمن).
/// </summary>
public sealed class RegisterPayoutHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    // ---------- مزيّفات داخلية ----------
    private sealed class PayoutRepoFake : IPayoutRepository
    {
        public int NextReceiptNo { get; set; } = 41;
        public List<Payout> Added { get; } = new();
        public IReadOnlyList<PayeePayoutTotal> TotalsToReturn { get; set; } = new List<PayeePayoutTotal>();

        public Task AddAsync(Payout payout, CancellationToken cancellationToken = default)
        {
            payout.SetId(1);   // محاكاة OUTPUT INSERTED.Id (InternalsVisibleTo)
            Added.Add(payout);
            return Task.CompletedTask;
        }
        public Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default) => Task.FromResult(NextReceiptNo);
        public Task<IReadOnlyList<PayeePayoutTotal>> GetTotalsByPayeeAsync(CancellationToken cancellationToken = default) => Task.FromResult(TotalsToReturn);
        public Task<IReadOnlyList<Payout>> GetForPayeeAsync(PayeeKind payeeKind, int payeeId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();   // مسار السجل — لا يُستدعى هنا
    }

    private sealed class LineRepoFake : IPayrollLineRepository
    {
        public IReadOnlyList<PayeeApprovedTotal> ApprovedTotals { get; set; } = new List<PayeeApprovedTotal>();

        public Task<IReadOnlyList<PayeeApprovedTotal>> GetApprovedTotalsByPayeeAsync(CancellationToken cancellationToken = default) => Task.FromResult(ApprovedTotals);
        public Task<IReadOnlyList<PayrollLine>> GetByRunAsync(int runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IReadOnlyList<PayrollLine> lines, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteComputedForRunAsync(int runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int lineId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, int>> GetCountsByRunAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TeacherRepoFake : ITeacherRepository
    {
        public Teacher? TeacherToReturn { get; set; }
        public Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(TeacherToReturn);
        public Task AddAsync(Teacher teacher, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Teacher teacher, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<TeacherListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class EmployeeRepoFake : IEmployeeRepository
    {
        public Employee? EmployeeToReturn { get; set; }
        public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(EmployeeToReturn);
        public Task AddAsync(Employee employee, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<EmployeeListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    // ---------- بناة ----------
    private static Teacher BuildTeacher(int id) => Teacher.Load(id, 70, null, null, Now, 1, null, null);
    private static Employee BuildEmployee(int id) => Employee.Load(id, 30, "محاسبة", null, Now, 1, null, null);

    private static (RegisterPayoutHandler handler, PayoutRepoFake payouts, FakeUnitOfWork uow) Build(
        long approved = 0, long paid = 0, bool teacherExists = true, int payeeId = 7)
    {
        var payouts = new PayoutRepoFake
        {
            NextReceiptNo = 41,
            TotalsToReturn = paid == 0
                ? new List<PayeePayoutTotal>()
                : new List<PayeePayoutTotal> { new(PayeeKind.Teacher, payeeId, paid) }
        };
        var lines = new LineRepoFake
        {
            ApprovedTotals = approved == 0
                ? new List<PayeeApprovedTotal>()
                : new List<PayeeApprovedTotal> { new(PayeeKind.Teacher, payeeId, approved) }
        };
        var teachers = new TeacherRepoFake { TeacherToReturn = teacherExists ? BuildTeacher(payeeId) : null };
        var uow = new FakeUnitOfWork();
        var treasuryAccounts = new FakeTreasuryAccountRepository();
        var handler = new RegisterPayoutHandler(payouts, lines, teachers, new EmployeeRepoFake(),
            treasuryAccounts, new FakeClock(), new FakeCurrentUserService(), uow, NullLogger<RegisterPayoutHandler>.Instance);
        return (handler, payouts, uow);
    }

    private static RegisterPayoutRequest PayTeacher(long amountCentimes, string? note = null, int teacherId = 7)
        => new(PayeeKind.Teacher, teacherId, null, null, 1, new DateOnly(2026, 8, 23), amountCentimes, note);

    // ---------- الاختبارات ----------
    [Fact]
    public async Task WithinBalance_Commits_WithSequentialReceiptNo()
    {
        var (handler, payouts, uow) = Build(approved: 100000, paid: 0);   // رصيد 1000.00 دج

        var result = await handler.ExecuteAsync(PayTeacher(60000));       // صرف 600.00 دج

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);                                    // معرف المزيّف (SetId)
        var payout = Assert.Single(payouts.Added);
        Assert.Equal(41, payout.ReceiptNo);                               // التسلسل من داخل المعاملة (مرآة D-105)
        Assert.Equal(60000, payout.AmountCentimes);
        Assert.Equal(PayeeKind.Teacher, payout.PayeeKind);
        Assert.Equal(7, payout.TeacherId);
        Assert.False(payout.IsCorrection);
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task OverBalance_WithoutNote_RejectedAsAdvance_BeforeAnyWrite()
    {
        var (handler, payouts, uow) = Build(approved: 100000, paid: 60000);   // الرصيد الجاري 400.00 دج

        var result = await handler.ExecuteAsync(PayTeacher(50000));           // 500.00 > 400.00 ← سلفة

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("سلفة", result.ErrorMessage);
        Assert.Contains("400.00", result.ErrorMessage);                   // الرصيد يُعرض بالدينار في الرسالة
        Assert.Empty(payouts.Added);
        Assert.Equal(0, uow.BeganCount);                                  // الحارس قبل فتح المعاملة
    }

    [Fact]
    public async Task OverBalance_WithNote_AllowedAsDocumentedAdvance()
    {
        var (handler, payouts, uow) = Build(approved: 100000, paid: 60000);

        var result = await handler.ExecuteAsync(PayTeacher(50000, "سلفة باتفاق المدير"));

        Assert.True(result.IsSuccess);                                    // سلفة حرة بملاحظة (D-116)
        Assert.Single(payouts.Added);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task NegativeCorrection_WithoutNote_Rejected_AndRolledBack()
    {
        var (handler, payouts, uow) = Build(approved: 100000);

        var result = await handler.ExecuteAsync(PayTeacher(-30000));      // قيد تصحيح بلا توثيق

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("ملاحظة", result.ErrorMessage);                   // حارس الكيان: القيد السالب يتطلب ملاحظة (س-5)
        Assert.Empty(payouts.Added);
        Assert.Equal(1, uow.BeganCount);                                  // الكيان يرمي داخل المعاملة…
        Assert.Equal(1, uow.RolledBackCount);                             // …فتُراجع ذرّياً
        Assert.Equal(0, uow.CommittedCount);
    }

    [Fact]
    public async Task ZeroAmount_Rejected_AndRolledBack()
    {
        var (handler, payouts, uow) = Build(approved: 100000);

        var result = await handler.ExecuteAsync(PayTeacher(0));

        Assert.False(result.IsSuccess);
        Assert.Contains("صفر", result.ErrorMessage);
        Assert.Empty(payouts.Added);
        Assert.Equal(1, uow.RolledBackCount);
    }

    [Fact]
    public async Task TeacherNotFound_Rejected_BeforeAnyWrite()
    {
        var (handler, payouts, uow) = Build(approved: 100000, teacherExists: false);

        var result = await handler.ExecuteAsync(PayTeacher(10000));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Empty(payouts.Added);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task NoApprovedYet_AnyPayoutIsAdvance_NeedsNote()
    {
        // لا معتمد إطلاقاً (المسودات لا تصنع ديناً — مصدر الرصيد كشوف معتمدة فقط) ⇒ أي صرف = سلفة
        var (handler, payouts, uow) = Build(approved: 0, paid: 0);

        var result = await handler.ExecuteAsync(PayTeacher(10000));

        Assert.False(result.IsSuccess);
        Assert.Contains("سلفة", result.ErrorMessage);
        Assert.Equal(0, uow.BeganCount);
    }
}