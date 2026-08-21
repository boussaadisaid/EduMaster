using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record CreateStreamRequest(int LevelId, string? Name);

public sealed class CreateStreamHandler
{
    private readonly IStreamRepository _streams;
    private readonly ILevelRepository _levels;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateStreamHandler> _logger;

    public CreateStreamHandler(IStreamRepository streams, ILevelRepository levels, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<CreateStreamHandler> logger)
    {
        _streams = streams;
        _levels = levels;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateStreamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult<int>.Failure("أدخل اسم الشعبة.", ErrorType.Validation);

        try
        {
            var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
            if (level is null)
                return OperationResult<int>.Failure("المستوى المحدد غير موجود.", ErrorType.Validation);
            if (!level.IsActive)
                return OperationResult<int>.Failure("لا يمكن إضافة شعبة لمستوى معطّل — فعّله أولاً.", ErrorType.BusinessRule);

            // الفرادة داخل المستوى الواحد فقط (لا عموماً)
            if (await _streams.AnyWithNameInLevelAsync(request.LevelId, request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("توجد شعبة بهذا الاسم في هذا المستوى بالفعل.", ErrorType.Conflict);

            var stream = Domain.Academic.Stream.Create(request.LevelId, request.Name, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _streams.AddAsync(stream, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(stream.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create stream {Name} in level {LevelId}", request.Name, request.LevelId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة الشعبة.", ErrorType.Unexpected);
        }
    }
}