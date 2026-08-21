using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record UpdateStreamRequest(int StreamId, string? Name);

public sealed class UpdateStreamHandler
{
    private readonly IStreamRepository _streams;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateStreamHandler> _logger;

    public UpdateStreamHandler(IStreamRepository streams, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateStreamHandler> logger)
    {
        _streams = streams;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateStreamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult.Failure("أدخل اسم الشعبة.", ErrorType.Validation);

        try
        {
            var stream = await _streams.GetByIdAsync(request.StreamId, cancellationToken);
            if (stream is null)
                return OperationResult.Failure("الشعبة غير موجودة.", ErrorType.NotFound);

            if (await _streams.AnyWithNameInLevelAsync(stream.LevelId, request.Name.Trim(), request.StreamId, cancellationToken))
                return OperationResult.Failure("توجد شعبة أخرى بهذا الاسم في هذا المستوى بالفعل.", ErrorType.Conflict);

            stream.Update(request.Name, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _streams.UpdateAsync(stream, cancellationToken);
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
            _logger.LogError(ex, "Failed to update stream {StreamId}", request.StreamId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الشعبة.", ErrorType.Unexpected);
        }
    }
}
   