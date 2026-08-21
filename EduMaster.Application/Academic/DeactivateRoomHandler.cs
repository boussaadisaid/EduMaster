using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record DeactivateRoomRequest(int RoomId);

public sealed class DeactivateRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateRoomHandler> _logger;

    public DeactivateRoomHandler(IRoomRepository rooms, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<DeactivateRoomHandler> logger)
    {
        _rooms = rooms;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(DeactivateRoomRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken);
            if (room is null)
                return OperationResult.Failure("القاعة غير موجودة.", ErrorType.NotFound);

            // ح-5: حارس البيانات التشغيلية — يُفعَّل فعلياً في F2 (الأفواج — القاعة اختيارية هناك دائماً)
            if (await _rooms.HasOperationalDataAsync(request.RoomId, cancellationToken))
                return OperationResult.Failure("لا يمكن تعطيل قاعة عليها بيانات تشغيلية (أفواج…) — تبقى للأرشيف.", ErrorType.BusinessRule);

            room.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _rooms.UpdateAsync(room, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deactivate room {RoomId}", request.RoomId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل القاعة.", ErrorType.Unexpected);
        }
    }
}