using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record CreateLevelRequest(string? Name, int SortOrder);

public sealed class CreateLevelHandler
{
    private readonly ILevelRepository _levels;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateLevelHandler> _logger;

    public CreateLevelHandler(ILevelRepository levels, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateLevelHandler> logger)
    {
        _levels = levels;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateLevelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult<int>.Failure("أدخل اسم المستوى.", ErrorType.Validation);

        try
        {
            // فحص الفرادة الودي قبل الاصطدام بالقيد (D-22)
            if (await _levels.AnyWithNameAsync(request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("يوجد مستوى بهذا الاسم بالفعل.", ErrorType.Conflict);

            var level = Level.Create(request.Name, request.SortOrder, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _levels.AddAsync(level, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(level.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create level {Name}", request.Name);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة المستوى.", ErrorType.Unexpected);
        }
    }
}