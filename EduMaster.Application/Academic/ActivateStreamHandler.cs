using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record ActivateStreamRequest(int StreamId);

public sealed class ActivateStreamHandler
{
    private readonly IStreamRepository _streams;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateStreamHandler> _logger;

    public ActivateStreamHandler(IStreamRepository streams, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateStreamHandler> logger)
    {
        _streams = streams;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivateStreamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var stream = await _streams.GetByIdAsync(request.StreamId, cancellationToken);
            if (stream is null)
                return OperationResult.Failure("الشعبة غير موجودة.", ErrorType.NotFound);

            // التفعيل دائم الجواز — حارس البيانات التشغيلية مكانه التعطيل فقط
            stream.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _streams.UpdateAsync(stream, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate stream {StreamId}", request.StreamId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل الشعبة.", ErrorType.Unexpected);
        }
    }
}