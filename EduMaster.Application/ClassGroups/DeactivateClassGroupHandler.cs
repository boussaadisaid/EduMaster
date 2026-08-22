using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

public sealed record DeactivateClassGroupRequest(int ClassGroupId);

public sealed class DeactivateClassGroupHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateClassGroupHandler> _logger;

    public DeactivateClassGroupHandler(IClassGroupRepository classGroups, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<DeactivateClassGroupHandler> logger)
    {
        _classGroups = classGroups;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(DeactivateClassGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var classGroup = await _classGroups.GetByIdAsync(request.ClassGroupId, cancellationToken);
            if (classGroup is null)
                return OperationResult.Failure("الفوج غير موجود.", ErrorType.NotFound);

            // حارس البيانات التشغيلية — يُفعَّل فعلياً في 2.4 (التسجيلات)
            if (await _classGroups.HasOperationalDataAsync(request.ClassGroupId, cancellationToken))
                return OperationResult.Failure("لا يمكن تعطيل فوج عليه بيانات تشغيلية (تسجيلات…) — يبقى للأرشيف.", ErrorType.BusinessRule);

            classGroup.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _classGroups.UpdateAsync(classGroup, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deactivate class group {ClassGroupId}", request.ClassGroupId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل الفوج.", ErrorType.Unexpected);
        }
    }
}