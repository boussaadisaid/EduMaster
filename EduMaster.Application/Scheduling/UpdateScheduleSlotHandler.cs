using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>
/// تعديل موعد (D-88): حصصه المستقبلية المجدولة تُلغى تلقائياً في نفس المعاملة —
/// القيمة المرجعة = عدد الملغاة (تُعرض للمستخدم) · المُقامة والملغاة يدوياً لا تُمسّان
/// </summary>
public sealed record UpdateScheduleSlotRequest(int SlotId, int DayOfWeek, TimeOnly StartTime, int DurationMinutes);

public sealed class UpdateScheduleSlotHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClassSessionRepository _sessions;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateScheduleSlotHandler> _logger;

    public UpdateScheduleSlotHandler(IClassGroupScheduleRepository schedules, IClassSessionRepository sessions, IClassGroupRepository classGroups, IAcademicYearRepository academicYears,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<UpdateScheduleSlotHandler> logger)
    {
        _schedules = schedules;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _sessions = sessions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(UpdateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult<int>.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var schedule = await _schedules.GetByIdAsync(request.SlotId, cancellationToken);
            if (schedule is null)
                return OperationResult<int>.Failure("الموعد غير موجود.", ErrorType.NotFound);
            var group = await _classGroups.GetByIdAsync(schedule.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult<int>.Failure("فوج الموعد غير موجود.", ErrorType.NotFound);
            if (group.AcademicYearId != currentYear.Id)
                return OperationResult<int>.Failure("لا يمكن تعديل موعد لفوج من سنة دراسية سابقة أو غير حالية.", ErrorType.BusinessRule);

            schedule.Update(request.DayOfWeek, request.StartTime, request.DurationMinutes, _clock.UtcNow, _currentUser.UserAccountId);

            // توقيت العمل المحلي لفلتر «المستقبلية» (StartsAt محلي) — والتدقيق يبقى UTC
            var localNow = DateTime.Now;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _schedules.UpdateAsync(schedule, cancellationToken);
            var cancelled = await _sessions.CancelFutureScheduledBySlotAsync(
                schedule.Id, localNow, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(cancelled);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update schedule slot {SlotId}", request.SlotId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تعديل الموعد.", ErrorType.Unexpected);
        }
    }
}