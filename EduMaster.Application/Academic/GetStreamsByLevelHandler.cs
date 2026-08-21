using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed class GetStreamsByLevelHandler
{
    private readonly IStreamRepository _streams;
    private readonly ILogger<GetStreamsByLevelHandler> _logger;

    public GetStreamsByLevelHandler(IStreamRepository streams, ILogger<GetStreamsByLevelHandler> logger)
    {
        _streams = streams;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<Domain.Academic.Stream>>> ExecuteAsync(int levelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _streams.GetByLevelIdAsync(levelId, cancellationToken);
            return OperationResult<IReadOnlyList<Domain.Academic.Stream>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load streams for level {LevelId}", levelId);
            return OperationResult<IReadOnlyList<Domain.Academic.Stream>>.Failure("حدث خطأ غير متوقع أثناء تحميل الشعب.", ErrorType.Unexpected);
        }
    }
}