using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;


namespace EduMaster.Application.Academic;


public sealed record ActivateLevelRequest(int LevelId);

public sealed class ActivateLevelHandler
{
    private readonly ILevelRepository _levels;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateLevelHandler> _logger;

    public ActivateLevelHandler(ILevelRepository levels, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateLevelHandler> logger)
    {
        _levels = levels;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivateLevelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
            if (level is null)
                return OperationResult.Failure("المستوى غير موجود.", ErrorType.NotFound);

            level.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _levels.UpdateAsync(level, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate level {LevelId}", request.LevelId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل المستوى.", ErrorType.Unexpected);
        }
    }
}