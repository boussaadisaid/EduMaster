

using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;


public sealed record DeactivateStreamRequest(int StreamId);

public sealed class DeactivateStreamHandler     
{
    private readonly IStreamRepository _streams;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateStreamHandler> _logger;

    public DeactivateStreamHandler(IStreamRepository streams, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<DeactivateStreamHandler> logger)
    {
        _streams = streams;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(DeactivateStreamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var stream = await _streams.GetByIdAsync(request.StreamId, cancellationToken);
            if (stream is null)
                return OperationResult.Failure("الشعبة غير موجودة.", ErrorType.NotFound);

            // ح-5: حارس البيانات التشغيلية — يُفعَّل فعلياً في F2 (الأفواج)
            if (await _streams.HasOperationalDataAsync(request.StreamId, cancellationToken))
                return OperationResult.Failure("لا يمكن تعطيل شعبة عليها بيانات تشغيلية (أفواج…) — يبقى للأرشيف.", ErrorType.BusinessRule); 
            stream.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _streams.UpdateAsync(stream, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deactivate stream {StreamId}", request.StreamId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل الشعبة.", ErrorType.Unexpected);
        }
    }
}