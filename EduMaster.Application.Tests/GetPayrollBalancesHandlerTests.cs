using EduMaster.Application.Abstractions.Repositories;
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
/// الرصيد الجاري (D-125/س-4): البقية = Σ المعتمد − Σ المصروف عبر التاريخ (الترحيل تلقائي) · سلفة مبكرة بلا معتمد تظهر سالبة ·
/// صفران كلاهما يُستبعد · المُصفَّى تماماً يظهر بصفر · الأكبر بقيةً أولاً · الاسم من القوائم وإلا سقوط «#المعرف».
/// </summary>
public sealed class GetPayrollBalancesHandlerTests
{
    // ---------- مزيّفات داخلية (الأسماء تُرجع قوائم فارغة ⇒ يُختبر مسار السقوط — الحساب هو صلب الدرس) ----------
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

    private sealed class PayoutRepoFake : IPayoutRepository
    {
        public IReadOnlyList<PayeePayoutTotal> TotalsToReturn { get; set; } = new List<PayeePayoutTotal>();
        public Task<IReadOnlyList<PayeePayoutTotal>> GetTotalsByPayeeAsync(CancellationToken cancellationToken = default) => Task.FromResult(TotalsToReturn);
        public Task AddAsync(Payout payout, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Payout>> GetForPayeeAsync(PayeeKind payeeKind, int payeeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TeacherRepoFake : ITeacherRepository
    {
        public Task<IEnumerable<TeacherListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<TeacherListItem>());
        public Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(Teacher teacher, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Teacher teacher, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class EmployeeRepoFake : IEmployeeRepository
    {
        public Task<IEnumerable<EmployeeListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<EmployeeListItem>());
        public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(Employee employee, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static GetPayrollBalancesHandler Build(IReadOnlyList<PayeeApprovedTotal> approved, IReadOnlyList<PayeePayoutTotal> paid)
        => new(new LineRepoFake { ApprovedTotals = approved }, new PayoutRepoFake { TotalsToReturn = paid },
            new TeacherRepoFake(), new EmployeeRepoFake(), NullLogger<GetPayrollBalancesHandler>.Instance);

    [Fact]
    public async Task CombinesApprovedMinusPaid_OrderedByBalanceDesc_SettledShownAsZero()
    {
        var handler = Build(
            new List<PayeeApprovedTotal>
            {
                new(PayeeKind.Teacher, 7, 525250),    // كشف سبتمبر: 5 252.50 دج
                new(PayeeKind.Employee, 3, 1200000),  // شهري ثابت: 12 000.00 دج
                new(PayeeKind.Teacher, 10, 500000),   // مُصفَّى تماماً ↓
            },
            new List<PayeePayoutTotal>
            {
                new(PayeeKind.Teacher, 7, 500000),    // صُرف له 5 000.00 (تقريب نقدي)
                new(PayeeKind.Teacher, 10, 500000),
            });

        var result = await handler.ExecuteAsync();

        Assert.True(result.IsSuccess);
        var items = result.Value!;
        Assert.Equal(3, items.Count);
        Assert.Equal(1200000, items[0].BalanceCentimes);            // الموظف أولاً — الأكبر بقيةً
        Assert.Equal(PayeeKind.Employee, items[0].PayeeKind);
        Assert.Equal(25250, items[1].BalanceCentimes);              // الترحيل: بقية سبتمبر حيّة
        Assert.Equal(7, items[1].PayeeId);
        Assert.Equal("أستاذ #7", items[1].PayeeName);               // سقوط الاسم (القائمة فارغة)
        Assert.Equal(0, items[2].BalanceCentimes);                  // المُصفَّى يظهر بصفر — له تاريخ مالي
        Assert.All(items, i => Assert.False(i.IsNegativeBalance));
    }

    [Fact]
    public async Task AdvanceWithoutApproved_AppearsNegative_AtTail()
    {
        var handler = Build(
            new List<PayeeApprovedTotal> { new(PayeeKind.Teacher, 7, 525250) },
            new List<PayeePayoutTotal>
            {
                new(PayeeKind.Teacher, 7, 500000),
                new(PayeeKind.Teacher, 8, 100000),                  // سلفة بلا أي كشف معتمد
            });

        var result = await handler.ExecuteAsync();

        var items = result.Value!;
        Assert.Equal(2, items.Count);
        Assert.Equal(-100000, items[1].BalanceCentimes);            // سالب = سلفة قائمة — في الذيل
        Assert.True(items[1].IsNegativeBalance);
        Assert.Equal(8, items[1].PayeeId);
    }

    [Fact]
    public async Task ZeroApprovedAndZeroPaid_Skipped()
    {
        // سطور يدوية متعادلة (+5000/−5000) في كشوف معتمدة ⇒ مجموعه صفر — لا حضور مالي يُذكر
        var handler = Build(
            new List<PayeeApprovedTotal> { new(PayeeKind.Teacher, 9, 0) },
            new List<PayeePayoutTotal>());

        var result = await handler.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}