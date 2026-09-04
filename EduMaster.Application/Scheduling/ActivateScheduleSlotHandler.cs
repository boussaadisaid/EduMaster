using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>تفعيل موعد — بلا كاسكيد (لا يُعيد إحياء ما أُلغي؛ التوليد التالي يلتقطه من جديد)</summary>
public sealed record ActivateScheduleSlotRequest(int SlotId);

public sealed class ActivateScheduleSlotHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateScheduleSlotHandler> _logger;

    public ActivateScheduleSlotHandler(IClassGroupScheduleRepository schedules, IClassGroupRepository classGroups, IAcademicYearRepository academicYears, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<ActivateScheduleSlotHandler> logger)
    {
        _schedules = schedules;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var schedule = await _schedules.GetByIdAsync(request.SlotId, cancellationToken);
            if (schedule is null)
                return OperationResult.Failure("الموعد غير موجود.", ErrorType.NotFound);
            var group = await _classGroups.GetByIdAsync(schedule.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult.Failure("فوج الموعد غير موجود.", ErrorType.NotFound);
            if (group.AcademicYearId != currentYear.Id)
                return OperationResult.Failure("لا يمكن تفعيل موعد لفوج من سنة دراسية سابقة أو غير حالية.", ErrorType.BusinessRule);

            schedule.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _schedules.UpdateAsync(schedule, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate schedule slot {SlotId}", request.SlotId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل الموعد.", ErrorType.Unexpected);
        }
    }
}