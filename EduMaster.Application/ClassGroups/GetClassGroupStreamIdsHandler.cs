using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

/// <summary>معرفات شعب فوج معيّن — لتعبئة محرر الفوج عند الفتح (فارغة = كل الشعب)</summary>
public sealed class GetClassGroupStreamIdsHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly ILogger<GetClassGroupStreamIdsHandler> _logger;

    public GetClassGroupStreamIdsHandler(IClassGroupRepository classGroups, ILogger<GetClassGroupStreamIdsHandler> logger)
    {
        _classGroups = classGroups;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<int>>> ExecuteAsync(
        int classGroupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var ids = await _classGroups.GetStreamIdsAsync(classGroupId, cancellationToken);
            return OperationResult<IReadOnlyList<int>>.Success(ids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load stream ids for class group {ClassGroupId}", classGroupId);
            return OperationResult<IReadOnlyList<int>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل شعب الفوج.", ErrorType.Unexpected);
        }
    }
}