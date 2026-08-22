using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

public sealed record CreateClassGroupRequest(
    int AcademicYearId,
    int LevelId,
    int SubjectId,
    int? TeacherId,
    int? RoomId,
    string? Name,
    int? Capacity,
    IReadOnlyList<int>? StreamIds);

public sealed class CreateClassGroupHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _years;
    private readonly ILevelRepository _levels;
    private readonly ISubjectRepository _subjects;
    private readonly IRoomRepository _rooms;
    private readonly ITeacherRepository _teachers;
    private readonly IStreamRepository _streams;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateClassGroupHandler> _logger;

    public CreateClassGroupHandler(IClassGroupRepository classGroups, IAcademicYearRepository years,
        ILevelRepository levels, ISubjectRepository subjects, IRoomRepository rooms, ITeacherRepository teachers,
        IStreamRepository streams, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateClassGroupHandler> logger)
    {
        _classGroups = classGroups;
        _years = years;
        _levels = levels;
        _subjects = subjects;
        _rooms = rooms;
        _teachers = teachers;
        _streams = streams;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateClassGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult<int>.Failure("أدخل اسم الفوج.", ErrorType.Validation);

        try
        {
            var year = await _years.GetByIdAsync(request.AcademicYearId, cancellationToken);
            if (year is null)
                return OperationResult<int>.Failure("السنة الدراسية المحددة غير موجودة.", ErrorType.Validation);
            if (!year.IsActive)
                return OperationResult<int>.Failure("لا يمكن إنشاء فوج في سنة معطّلة.", ErrorType.BusinessRule);

            var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
            if (level is null)
                return OperationResult<int>.Failure("المستوى المحدد غير موجود.", ErrorType.Validation);
            if (!level.IsActive)
                return OperationResult<int>.Failure("لا يمكن إنشاء فوج لمستوى معطّل — فعّله أولاً.", ErrorType.BusinessRule);

            var subject = await _subjects.GetByIdAsync(request.SubjectId, cancellationToken);
            if (subject is null)
                return OperationResult<int>.Failure("المادة المحددة غير موجودة.", ErrorType.Validation);
            if (!subject.IsActive)
                return OperationResult<int>.Failure("لا يمكن إنشاء فوج لمادة معطّلة — فعّلها أولاً.", ErrorType.BusinessRule);

            if (request.TeacherId is not null)
            {
                // GetByIdAsync يستثني الملفات المحذوفة منطقياً (IsDeleted = 0)
                var teacher = await _teachers.GetByIdAsync(request.TeacherId.Value, cancellationToken);
                if (teacher is null)
                    return OperationResult<int>.Failure("الأستاذ المحدد غير موجود.", ErrorType.Validation);
            }

            if (request.RoomId is not null)
            {
                var room = await _rooms.GetByIdAsync(request.RoomId.Value, cancellationToken);
                if (room is null)
                    return OperationResult<int>.Failure("القاعة المحددة غير موجودة.", ErrorType.Validation);
                if (!room.IsActive)
                    return OperationResult<int>.Failure("القاعة المحددة معطّلة — فعّلها أو اختر غيرها.", ErrorType.BusinessRule);
            }

            // D-48: شعب الفوج — فارغة = يقبل كل شعب المستوى · غير الفارغة يجب أن تتبع المستوى وتكون فعّالة
            var requestedStreamIds = (request.StreamIds ?? Array.Empty<int>()).Distinct().ToList();
            if (requestedStreamIds.Count > 0)
            {
                var levelStreams = await _streams.GetByLevelIdAsync(request.LevelId, cancellationToken);
                var levelStreamIds = levelStreams.Select(s => s.Id).ToHashSet();

                if (requestedStreamIds.Any(id => !levelStreamIds.Contains(id)))
                    return OperationResult<int>.Failure("إحدى الشعب المحددة لا تتبع مستوى الفوج.", ErrorType.Validation);
                if (levelStreams.Any(s => requestedStreamIds.Contains(s.Id) && !s.IsActive))
                    return OperationResult<int>.Failure("لا يمكن قصر الفوج على شعبة معطّلة — فعّلها أو أزلها من القائمة.", ErrorType.BusinessRule);
            }

            // فحص الفرادة الودي قبل الاصطدام بالقيد (D-22) — الفرادة داخل السنة الواحدة
            if (await _classGroups.AnyWithNameInYearAsync(request.AcademicYearId, request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("يوجد فوج بهذا الاسم في هذه السنة بالفعل.", ErrorType.Conflict);

            var classGroup = Domain.ClassGroups.ClassGroup.Create(
                request.AcademicYearId, request.LevelId, request.SubjectId,
                request.TeacherId, request.RoomId, request.Name, request.Capacity,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _classGroups.AddAsync(classGroup, cancellationToken);
            if (requestedStreamIds.Count > 0)
                await _classGroups.ReplaceStreamsAsync(classGroup.Id, classGroup.LevelId, requestedStreamIds, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(classGroup.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create class group {Name} for year {AcademicYearId}", request.Name, request.AcademicYearId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء الفوج.", ErrorType.Unexpected);
        }
    }
}