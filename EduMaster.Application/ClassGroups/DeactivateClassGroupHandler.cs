using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

public sealed record DeactivateClassGroupRequest(int Id);

/// <summary>
/// تعطيل الفوج — حارس D-55 (مسجَّلون نشطون يمنعون) ثم كاسكيد D-90:
/// حصصه المستقبلية المجدولة تُلغى في نفس المعاملة · القيمة المرجعة = عدد الملغاة (تُعرض للمستخدم)
/// </summary>
public sealed class DeactivateClassGroupHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly IClassSessionRepository _sessions;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateClassGroupHandler> _logger;

    public DeactivateClassGroupHandler(IClassGroupRepository classGroups, IClassSessionRepository sessions,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<DeactivateClassGroupHandler> logger)
    {
        _classGroups = classGroups;
        _sessions = sessions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(DeactivateClassGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var classGroup = await _classGroups.GetByIdAsync(request.Id, cancellationToken);
            if (classGroup is null)
                return OperationResult<int>.Failure("الفوج غير موجود.", ErrorType.NotFound);

            if (!classGroup.IsActive)
                return OperationResult<int>.Success(0);

            // D-55: فوج فيه مسجَّلون نشطون لا يُعطَّل (مفعَّل منذ 2.4)
            if (await _classGroups.HasOperationalDataAsync(request.Id, cancellationToken))
                return OperationResult<int>.Failure("لا يمكن تعطيل الفوج — فيه مسجَّلون نشطون. اسحبهم أو انقلهم أولاً.", ErrorType.BusinessRule);

            classGroup.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            var localNow = DateTime.Now;   // StartsAt توقيت عمل محلي

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _classGroups.UpdateAsync(classGroup, cancellationToken);
            var cancelledSessions = await _sessions.CancelFutureScheduledByGroupAsync(
                request.Id, localNow, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(cancelledSessions);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deactivate class group {ClassGroupId}", request.Id);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تعطيل الفوج.", ErrorType.Unexpected);
        }
    }
}