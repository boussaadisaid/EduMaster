using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

public sealed record ActivateClassGroupRequest(int ClassGroupId);

public sealed class ActivateClassGroupHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateClassGroupHandler> _logger;

    public ActivateClassGroupHandler(IClassGroupRepository classGroups, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateClassGroupHandler> logger)
    {
        _classGroups = classGroups;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivateClassGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var classGroup = await _classGroups.GetByIdAsync(request.ClassGroupId, cancellationToken);
            if (classGroup is null)
                return OperationResult.Failure("الفوج غير موجود.", ErrorType.NotFound);

            // التفعيل دائم الجواز — حارس البيانات التشغيلية مكانه التعطيل فقط (D-45)
            classGroup.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _classGroups.UpdateAsync(classGroup, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate class group {ClassGroupId}", request.ClassGroupId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل الفوج.", ErrorType.Unexpected);
        }
    }
}