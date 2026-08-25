using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>حذف يوم عمل — التصحيح الوحيد في سجل «كتابة فقط»: حذف اليوم وإعادة تسجيله (لا Update — D-115)</summary>
public sealed record RemoveWorkLogDayRequest(int WorkLogEntryId);

public sealed class RemoveWorkLogDayHandler
{
    private readonly IEmployeeWorkLogRepository _workLog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveWorkLogDayHandler> _logger;

    public RemoveWorkLogDayHandler(
        IEmployeeWorkLogRepository workLog,
        IUnitOfWork unitOfWork,
        ILogger<RemoveWorkLogDayHandler> logger)
    {
        _workLog = workLog;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(RemoveWorkLogDayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            var deleted = await _workLog.DeleteAsync(request.WorkLogEntryId, cancellationToken);
            if (deleted == 0)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                return OperationResult.Failure("يوم العمل غير موجود — ربما حُذف مسبقاً.", ErrorType.NotFound);
            }
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to remove work log entry {WorkLogEntryId}", request.WorkLogEntryId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف يوم العمل.", ErrorType.Unexpected);
        }
    }
}