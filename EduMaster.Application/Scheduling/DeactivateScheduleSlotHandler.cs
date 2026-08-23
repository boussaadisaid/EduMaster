using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>تعطيل موعد (D-88): نفس كاسكيد التعديل — القيمة المرجعة = عدد الحصص الملغاة</summary>
public sealed record DeactivateScheduleSlotRequest(int SlotId);

public sealed class DeactivateScheduleSlotHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly IClassSessionRepository _sessions;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateScheduleSlotHandler> _logger;

    public DeactivateScheduleSlotHandler(IClassGroupScheduleRepository schedules, IClassSessionRepository sessions,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<DeactivateScheduleSlotHandler> logger)
    {
        _schedules = schedules;
        _sessions = sessions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(DeactivateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var schedule = await _schedules.GetByIdAsync(request.SlotId, cancellationToken);
            if (schedule is null)
                return OperationResult<int>.Failure("الموعد غير موجود.", ErrorType.NotFound);

            if (!schedule.IsActive)
                return OperationResult<int>.Success(0);

            schedule.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            var localNow = DateTime.Now;   // StartsAt توقيت عمل محلي

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _schedules.UpdateAsync(schedule, cancellationToken);
            var cancelled = await _sessions.CancelFutureScheduledBySlotAsync(
                schedule.Id, localNow, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(cancelled);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deactivate schedule slot {SlotId}", request.SlotId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تعطيل الموعد.", ErrorType.Unexpected);
        }
    }
}