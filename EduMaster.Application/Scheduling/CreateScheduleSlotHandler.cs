using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>إضافة موعد أسبوعي (D-86) — التعارض يُفحص مسبقاً بالقراءة ولا يمنع هنا (D-89)</summary>
public sealed record CreateScheduleSlotRequest(int ClassGroupId, int DayOfWeek, TimeOnly StartTime, int DurationMinutes);

public sealed class CreateScheduleSlotHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateScheduleSlotHandler> _logger;

    public CreateScheduleSlotHandler(IClassGroupScheduleRepository schedules, IClassGroupRepository classGroups, IAcademicYearRepository academicYears,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<CreateScheduleSlotHandler> logger)
    {
        _schedules = schedules;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult<int>.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var group = await _classGroups.GetByIdAsync(request.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult<int>.Failure("الفوج غير موجود.", ErrorType.NotFound);
            if (!group.IsActive)
                return OperationResult<int>.Failure("الفوج معطّل — لا يقبل مواعيد.", ErrorType.BusinessRule);
            if (group.AcademicYearId != currentYear.Id)
                return OperationResult<int>.Failure("لا يمكن إضافة موعد لفوج من سنة دراسية سابقة أو غير حالية.", ErrorType.BusinessRule);

            var schedule = Domain.Scheduling.ClassGroupSchedule.Create(
                request.ClassGroupId, request.DayOfWeek, request.StartTime, request.DurationMinutes,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _schedules.AddAsync(schedule, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(schedule.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create schedule slot for class group {ClassGroupId}", request.ClassGroupId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة الموعد.", ErrorType.Unexpected);
        }
    }
}