using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed class GetRoomsHandler
{
    private readonly IRoomRepository _rooms;
    private readonly ILogger<GetRoomsHandler> _logger;

    public GetRoomsHandler(IRoomRepository rooms, ILogger<GetRoomsHandler> logger)
    {
        _rooms = rooms;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<Room>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _rooms.GetAllAsync(cancellationToken);
            return OperationResult<IReadOnlyList<Room>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rooms");
            return OperationResult<IReadOnlyList<Room>>.Failure("حدث خطأ غير متوقع أثناء تحميل القاعات.", ErrorType.Unexpected);
        }
    }
}