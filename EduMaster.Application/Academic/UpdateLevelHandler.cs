using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.Academic;
public sealed record UpdateLevelRequest(int LevelId, string? Name, int SortOrder);

public sealed class UpdateLevelHandler
{
    private readonly ILevelRepository _levels;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateLevelHandler> _logger;

    public UpdateLevelHandler(ILevelRepository levels, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateLevelHandler> logger)
    {
        _levels = levels;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateLevelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult.Failure("أدخل اسم المستوى.", ErrorType.Validation);

        try
        {
            var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
            if (level is null)
                return OperationResult.Failure("المستوى غير موجود.", ErrorType.NotFound);

            // فرادة مع استثناء الذات (نمط D-27)
            if (await _levels.AnyWithNameAsync(request.Name.Trim(), request.LevelId, cancellationToken))
                return OperationResult.Failure("يوجد مستوى آخر بهذا الاسم بالفعل.", ErrorType.Conflict);

            level.Update(request.Name, request.SortOrder, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _levels.UpdateAsync(level, cancellationToken);
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
            _logger.LogError(ex, "Failed to update level {LevelId}", request.LevelId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل المستوى.", ErrorType.Unexpected);
        }
    }
}