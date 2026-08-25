using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>قائمة الكشوف لشاشة «💼 الأجور» (D-116) — الأحدث فترةً أولاً + عدد سطور كل كشف.</summary>
public sealed class GetPayrollRunsHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly ILogger<GetPayrollRunsHandler> _logger;

    public GetPayrollRunsHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        ILogger<GetPayrollRunsHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<PayrollRunListItem>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var runs = await _runs.GetAllAsync(cancellationToken);
            var counts = await _lines.GetCountsByRunAsync(cancellationToken);

            IReadOnlyList<PayrollRunListItem> items = runs
                .OrderByDescending(r => r.PeriodStart)
                .ThenByDescending(r => r.Id)
                .Select(r => new PayrollRunListItem(r.Id, r.PeriodStart, r.PeriodEnd, r.Status, r.TotalCentimes,
                    counts.GetValueOrDefault(r.Id), r.CreatedAtUtc, r.ApprovedAtUtc))
                .ToList();

            return OperationResult<IReadOnlyList<PayrollRunListItem>>.Success(items);
        }
        catch (OperationCanceledException) { throw; }   // D-64: الإلغاء ليس خطأً
        catch (Exception ex) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("أُلغي تحميل الكشوف.", ex, cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list payroll runs");
            return OperationResult<IReadOnlyList<PayrollRunListItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل الكشوف.", ErrorType.Unexpected);
        }
    }
}