using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// حذف سطر يدوي من مسودة (س-8) — مكافأة/خصم أُدخل بالخطأ. المحسوبة لا تُحذف فردياً أبداً (تُزال بإعادة الحساب فقط — روح D-101) —
/// حارس «يدوي فقط + مسودة فقط + عضو في الكشف» في الكيان.
/// </summary>
public sealed record RemoveManualPayrollLineRequest(int RunId, int LineId);

public sealed class RemoveManualPayrollLineHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveManualPayrollLineHandler> _logger;

    public RemoveManualPayrollLineHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        IUnitOfWork unitOfWork,
        ILogger<RemoveManualPayrollLineHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(RemoveManualPayrollLineRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
                return OperationResult.Failure("الكشف غير موجود.", ErrorType.NotFound);
            if (run.IsApproved)
                return OperationResult.Failure("الكشف معتمد ويقفل نهائياً — لا يمكن حذف سطوره.", ErrorType.Conflict);

            run.LoadLines(await _lines.GetByRunAsync(run.Id, cancellationToken));

            var line = run.Lines.FirstOrDefault(l => l.Id == request.LineId);
            if (line is null)
                return OperationResult.Failure("السطر غير موجود في هذا الكشف.", ErrorType.NotFound);

            run.RemoveManualLine(line);   // يُنقص الإجمالي — وحارس «يدوي + مسودة» داخله

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _lines.DeleteAsync(line.Id, cancellationToken);
            await _runs.UpdateAsync(run, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while removing manual line {LineId} from payroll run {RunId} (D-121 trap)", request.LineId, request.RunId);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to remove manual line {LineId} from payroll run {RunId}", request.LineId, request.RunId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف السطر اليدوي.", ErrorType.Unexpected);
        }
    }
}