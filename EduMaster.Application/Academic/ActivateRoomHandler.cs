using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record ActivateRoomRequest(int RoomId);

public sealed class ActivateRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateRoomHandler> _logger;

    public ActivateRoomHandler(IRoomRepository rooms, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateRoomHandler> logger)
    {
        _rooms = rooms;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivateRoomRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken);
            if (room is null)
                return OperationResult.Failure("القاعة غير موجودة.", ErrorType.NotFound);

            // التفعيل دائم الجواز — لا حارس تشغيلية هنا
            room.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _rooms.UpdateAsync(room, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate room {RoomId}", request.RoomId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل القاعة.", ErrorType.Unexpected);
        }
    }
}