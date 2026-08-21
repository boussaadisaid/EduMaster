using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record UpdateRoomRequest(int RoomId, string? Name, int? Capacity);

public sealed class UpdateRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateRoomHandler> _logger;

    public UpdateRoomHandler(IRoomRepository rooms, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateRoomHandler> logger)
    {
        _rooms = rooms;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateRoomRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult.Failure("أدخل اسم القاعة.", ErrorType.Validation);

        try
        {
            var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken);
            if (room is null)
                return OperationResult.Failure("القاعة غير موجودة.", ErrorType.NotFound);

            // فرادة مع استثناء الذات (نمط D-27)
            if (await _rooms.AnyWithNameAsync(request.Name.Trim(), request.RoomId, cancellationToken))
                return OperationResult.Failure("توجد قاعة أخرى بهذا الاسم بالفعل.", ErrorType.Conflict);

            room.Update(request.Name, request.Capacity, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _rooms.UpdateAsync(room, cancellationToken);
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
            _logger.LogError(ex, "Failed to update room {RoomId}", request.RoomId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل القاعة.", ErrorType.Unexpected);
        }
    }
}