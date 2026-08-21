using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed class GetLevelsHandler
{
    private readonly ILevelRepository _levels;
    private readonly ILogger<GetLevelsHandler> _logger;

    public GetLevelsHandler(ILevelRepository levels, ILogger<GetLevelsHandler> logger)
    {
        _levels = levels;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<Level>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _levels.GetAllAsync(cancellationToken);
            return OperationResult<IReadOnlyList<Level>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load levels");
            return OperationResult<IReadOnlyList<Level>>.Failure("حدث خطأ غير متوقع أثناء تحميل المستويات.", ErrorType.Unexpected);
        }
    }
}