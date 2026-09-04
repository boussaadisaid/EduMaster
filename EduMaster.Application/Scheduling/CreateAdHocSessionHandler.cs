using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>حصة استثنائية في أي وقت (D-87 — بلا مصدر) · تُسمح بأثر رجعي (توثيق حصة فاتت جدولتها)</summary>
public sealed record CreateAdHocSessionRequest(int ClassGroupId, DateTime StartsAt, int DurationMinutes, string? Topic);

public sealed class CreateAdHocSessionHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAdHocSessionHandler> _logger;

    public CreateAdHocSessionHandler(IClassSessionRepository sessions, IClassGroupRepository classGroups, IAcademicYearRepository academicYears,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<CreateAdHocSessionHandler> logger)
    {
        _sessions = sessions;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateAdHocSessionRequest request, CancellationToken cancellationToken = default)
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
                return OperationResult<int>.Failure("الفوج معطّل — لا يقبل حصصاً.", ErrorType.BusinessRule);
            if (group.AcademicYearId != currentYear.Id)
                return OperationResult<int>.Failure("لا يمكن إنشاء حصة تشغيلية لفوج من سنة دراسية سابقة أو غير حالية.", ErrorType.BusinessRule);

            // فرادة التوقيت الودية قبل الاصطدام بالفهرس الفريد (D-22/D-87)
            if (await _sessions.AnyExistsAtAsync(request.ClassGroupId, request.StartsAt, null, cancellationToken))
                return OperationResult<int>.Failure("توجد حصة لهذا الفوج في هذا التوقيت بالفعل.", ErrorType.Conflict);

            var session = Domain.Scheduling.ClassSession.Create(
                request.ClassGroupId, null, request.StartsAt, request.DurationMinutes, request.Topic,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _sessions.AddAsync(session, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(session.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create ad-hoc session for class group {ClassGroupId}", request.ClassGroupId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء برمجة الحصة الاستثنائية.", ErrorType.Unexpected);
        }
    }
}