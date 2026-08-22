using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

/// <summary>الهوية (السنة/المستوى/المادة) لا تُعدَّل بعد الإنشاء — التغيير الجوهري = فوج جديد وتعطيل القديم</summary>
public sealed record UpdateClassGroupRequest(
    int ClassGroupId,
    int? TeacherId,
    int? RoomId,
    string? Name,
    int? Capacity,
    IReadOnlyList<int>? StreamIds);

public sealed class UpdateClassGroupHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly IRoomRepository _rooms;
    private readonly ITeacherRepository _teachers;
    private readonly IStreamRepository _streams;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateClassGroupHandler> _logger;

    public UpdateClassGroupHandler(IClassGroupRepository classGroups, IRoomRepository rooms, ITeacherRepository teachers,
        IStreamRepository streams, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateClassGroupHandler> logger)
    {
        _classGroups = classGroups;
        _rooms = rooms;
        _teachers = teachers;
        _streams = streams;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateClassGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult.Failure("أدخل اسم الفوج.", ErrorType.Validation);

        try
        {
            var classGroup = await _classGroups.GetByIdAsync(request.ClassGroupId, cancellationToken);
            if (classGroup is null)
                return OperationResult.Failure("الفوج غير موجود.", ErrorType.NotFound);

            // فرادة مع استثناء الذات (نمط D-27) — داخل سنة الفوج الثابتة
            if (await _classGroups.AnyWithNameInYearAsync(classGroup.AcademicYearId, request.Name.Trim(), request.ClassGroupId, cancellationToken))
                return OperationResult.Failure("يوجد فوج آخر بهذا الاسم في هذه السنة بالفعل.", ErrorType.Conflict);

            if (request.TeacherId is not null)
            {
                var teacher = await _teachers.GetByIdAsync(request.TeacherId.Value, cancellationToken);
                if (teacher is null)
                    return OperationResult.Failure("الأستاذ المحدد غير موجود.", ErrorType.Validation);
            }

            if (request.RoomId is not null)
            {
                var room = await _rooms.GetByIdAsync(request.RoomId.Value, cancellationToken);
                if (room is null)
                    return OperationResult.Failure("القاعة المحددة غير موجودة.", ErrorType.Validation);
                if (!room.IsActive)
                    return OperationResult.Failure("القاعة المحددة معطّلة — فعّلها أو اختر غيرها.", ErrorType.BusinessRule);
            }

            // D-48: الشعب تُطابَق مع مستوى الفوج الثابت (لا يتغير بالتعديل)
            var requestedStreamIds = (request.StreamIds ?? Array.Empty<int>()).Distinct().ToList();
            if (requestedStreamIds.Count > 0)
            {
                var levelStreams = await _streams.GetByLevelIdAsync(classGroup.LevelId, cancellationToken);
                var levelStreamIds = levelStreams.Select(s => s.Id).ToHashSet();

                if (requestedStreamIds.Any(id => !levelStreamIds.Contains(id)))
                    return OperationResult.Failure("إحدى الشعب المحددة لا تتبع مستوى الفوج.", ErrorType.Validation);
                if (levelStreams.Any(s => requestedStreamIds.Contains(s.Id) && !s.IsActive))
                    return OperationResult.Failure("لا يمكن قصر الفوج على شعبة معطّلة — فعّلها أو أزلها من القائمة.", ErrorType.BusinessRule);
            }

            classGroup.Update(request.Name, request.RoomId, request.Capacity, _clock.UtcNow, _currentUser.UserAccountId);
            if (request.TeacherId != classGroup.TeacherId)
                classGroup.AssignTeacher(request.TeacherId, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _classGroups.UpdateAsync(classGroup, cancellationToken);
            await _classGroups.ReplaceStreamsAsync(classGroup.Id, classGroup.LevelId, requestedStreamIds, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update class group {ClassGroupId}", request.ClassGroupId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الفوج.", ErrorType.Unexpected);
        }
    }
}