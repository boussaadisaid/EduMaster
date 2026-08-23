using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>جدول استعمال الزمن (D-86): الفعّالة افتراضياً — includeInactive يُدرج المعطّلة باهتةً لتُفعَّل منها</summary>
public sealed class GetTimetableHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly ILogger<GetTimetableHandler> _logger;

    public GetTimetableHandler(IClassGroupScheduleRepository schedules, ILogger<GetTimetableHandler> logger)
    {
        _schedules = schedules;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ScheduleSlotItem>>> ExecuteAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _schedules.GetForTimetableAsync(includeInactive, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ScheduleSlotItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Timetable load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the weekly timetable (includeInactive: {IncludeInactive})", includeInactive);
            return OperationResult<IReadOnlyList<ScheduleSlotItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل جدول استعمال الزمن.", ErrorType.Unexpected);
        }
    }
}