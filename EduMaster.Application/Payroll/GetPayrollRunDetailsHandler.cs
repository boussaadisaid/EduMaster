using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>تفاصيل كشف (رأسه + سطوره) — المحسوبة أولاً ثم اليدوية، وبترتيب الإنشاء داخل كل صنف · السطر يحمل معرف مستفيده (5.3-هـ: الصرف).</summary>
public sealed record GetPayrollRunDetailsRequest(int RunId);

public sealed class GetPayrollRunDetailsHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly ILogger<GetPayrollRunDetailsHandler> _logger;

    public GetPayrollRunDetailsHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        ILogger<GetPayrollRunDetailsHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _logger = logger;
    }

    public async Task<OperationResult<PayrollRunDetails>> ExecuteAsync(GetPayrollRunDetailsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
                return OperationResult<PayrollRunDetails>.Failure("الكشف غير موجود.", ErrorType.NotFound);

            var lines = await _lines.GetByRunAsync(run.Id, cancellationToken);

            var runItem = new PayrollRunListItem(run.Id, run.PeriodStart, run.PeriodEnd, run.Status,
                run.TotalCentimes, lines.Count, run.CreatedAtUtc, run.ApprovedAtUtc);

            var lineItems = lines
                .OrderBy(l => l.SourceKind)   // محسوبة (1) ثم يدوية (2)
                .ThenBy(l => l.Id)
                .Select(l => new PayrollLineItem(l.Id, l.RunId, l.PayeeKind, l.TeacherId, l.EmployeeId, l.PayeeName, l.PolicyId, l.Kind,
                    l.RateCentimes, l.Percentage, l.CountsUnjustifiedAbsent, l.Quantity, l.SourceKind, l.Details, l.AmountCentimes))
                .ToList();

            return OperationResult<PayrollRunDetails>.Success(new PayrollRunDetails(runItem, lineItems));
        }
        catch (OperationCanceledException) { throw; }   // D-64: الإلغاء ليس خطأً
        catch (Exception ex) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("أُلغي تحميل الكشف.", ex, cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load payroll run {RunId}", request.RunId);
            return OperationResult<PayrollRunDetails>.Failure("حدث خطأ غير متوقع أثناء تحميل الكشف.", ErrorType.Unexpected);
        }
    }
}