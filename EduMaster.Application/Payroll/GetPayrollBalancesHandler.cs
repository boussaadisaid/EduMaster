using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// أرصدة المستفيدين الجارية (5.3/س-4): البقية = Σ المعتمد (كشوف معتمدة فقط) − Σ المصروف (كل الإيصالات) —
/// من له صرف بلا معتمد (سلفة مبكرة) يظهر أيضاً · من صفران كلاهما لا يظهر · الأكبر بقيةً أولاً (عرف GetDebtorsHandler) · السالب = سلفة قائمة.
/// </summary>
public sealed class GetPayrollBalancesHandler
{
    private readonly IPayrollLineRepository _lines;
    private readonly IPayoutRepository _payouts;
    private readonly ITeacherRepository _teachers;
    private readonly IEmployeeRepository _employees;
    private readonly ILogger<GetPayrollBalancesHandler> _logger;

    public GetPayrollBalancesHandler(
        IPayrollLineRepository lines,
        IPayoutRepository payouts,
        ITeacherRepository teachers,
        IEmployeeRepository employees,
        ILogger<GetPayrollBalancesHandler> logger)
    {
        _lines = lines;
        _payouts = payouts;
        _teachers = teachers;
        _employees = employees;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<PayeeBalanceItem>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var approvedTotals = await _lines.GetApprovedTotalsByPayeeAsync(cancellationToken);
            var paidTotals = await _payouts.GetTotalsByPayeeAsync(cancellationToken);

            var teacherNames = (await _teachers.SearchAsync(null, cancellationToken)).ToDictionary(t => t.Id, t => t.FullName);
            var employeeNames = (await _employees.SearchAsync(null, cancellationToken)).ToDictionary(e => e.Id, e => e.FullName);

            var items = approvedTotals.Select(a => (a.PayeeKind, a.PayeeId))
                .Union(paidTotals.Select(p => (p.PayeeKind, p.PayeeId)))
                .Select(key =>
                {
                    var approved = approvedTotals.FirstOrDefault(t => t.PayeeKind == key.PayeeKind && t.PayeeId == key.PayeeId)?.TotalCentimes ?? 0;
                    var paid = paidTotals.FirstOrDefault(t => t.PayeeKind == key.PayeeKind && t.PayeeId == key.PayeeId)?.TotalCentimes ?? 0;
                    if (approved == 0 && paid == 0)
                        return null;   // لا حضور مالي — لا سطر

                    var name = key.PayeeKind == PayeeKind.Teacher
                        ? teacherNames.GetValueOrDefault(key.PayeeId, $"أستاذ #{key.PayeeId}")
                        : employeeNames.GetValueOrDefault(key.PayeeId, $"موظف #{key.PayeeId}");

                    return new PayeeBalanceItem(key.PayeeKind, key.PayeeId, name, approved, paid);
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .OrderByDescending(item => item.BalanceCentimes)   // الأكبر بقيةً أولاً — السالب (سلفة) في الذيل
                .ToList();

            return OperationResult<IReadOnlyList<PayeeBalanceItem>>.Success(items);
        }
        catch (OperationCanceledException) { throw; }   // D-64: الإلغاء ليس خطأً
        catch (Exception ex) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("أُلغي تحميل الأرصدة.", ex, cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute payroll balances");
            return OperationResult<IReadOnlyList<PayeeBalanceItem>>.Failure("حدث خطأ غير متوقع أثناء حساب الأرصدة.", ErrorType.Unexpected);
        }
    }
}