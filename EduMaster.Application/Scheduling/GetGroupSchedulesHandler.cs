using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>كل مواعيد فوج (فعّالة ومعطّلة) — لإدارتها</summary>
public sealed class GetGroupSchedulesHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly ILogger<GetGroupSchedulesHandler> _logger;

    public GetGroupSchedulesHandler(IClassGroupScheduleRepository schedules, ILogger<GetGroupSchedulesHandler> logger)
    {
        _schedules = schedules;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ScheduleSlotItem>>> ExecuteAsync(int classGroupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _schedules.GetForGroupAsync(classGroupId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ScheduleSlotItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Group schedules load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load schedules for class group {ClassGroupId}", classGroupId);
            return OperationResult<IReadOnlyList<ScheduleSlotItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل مواعيد الفوج.", ErrorType.Unexpected);
        }
    }
}