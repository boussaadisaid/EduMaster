using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record CreateRoomRequest(string? Name, int? Capacity);

public sealed class CreateRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateRoomHandler> _logger;

    public CreateRoomHandler(IRoomRepository rooms, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateRoomHandler> logger)
    {
        _rooms = rooms;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult<int>.Failure("أدخل اسم القاعة.", ErrorType.Validation);

        try
        {
            // فحص الفرادة الودي قبل الاصطدام بالقيد (D-22)
            if (await _rooms.AnyWithNameAsync(request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("توجد قاعة بهذا الاسم بالفعل.", ErrorType.Conflict);

            var room = Room.Create(request.Name, request.Capacity, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _rooms.AddAsync(room, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(room.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create room {Name}", request.Name);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة القاعة.", ErrorType.Unexpected);
        }
    }
}