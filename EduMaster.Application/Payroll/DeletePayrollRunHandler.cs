using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// حذف مسودة كشف (D-116): المسودة مسرح عمل قابل للرمي — المعتمد لا يُحذف أبداً (الخطأ بعده = صرف تسوية في 5.3).
/// سطور المسودة تتبعها بتتابع الحذف (ON DELETE CASCADE في 016).
/// </summary>
public sealed record DeletePayrollRunRequest(int RunId);

public sealed class DeletePayrollRunHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeletePayrollRunHandler> _logger;

    public DeletePayrollRunHandler(
        IPayrollRunRepository runs,
        IUnitOfWork unitOfWork,
        ILogger<DeletePayrollRunHandler> logger)
    {
        _runs = runs;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(DeletePayrollRunRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
                return OperationResult.Failure("الكشف غير موجود.", ErrorType.NotFound);
            if (run.IsApproved)
                return OperationResult.Failure("الكشف معتمد ويقفل نهائياً — لا يمكن حذفه.", ErrorType.Conflict);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _runs.DeleteAsync(run.Id, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while deleting payroll run {RunId} (D-121 trap)", request.RunId);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to delete payroll run {RunId}", request.RunId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف المسودة.", ErrorType.Unexpected);
        }
    }
}