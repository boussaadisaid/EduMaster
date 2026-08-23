using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>
/// فحص تعارض قاعة/أستاذ قبل حفظ الموعد (D-89 — تحذير غير مانع): الواجهة تستدعيه أولاً،
/// وعند وجود تعارضات تعرضها بتأكيد «متابعة؟» ثم تستدعي الحفظ — الحفظ نفسه لا يمنع.
/// </summary>
public sealed record GetScheduleConflictsRequest(int ClassGroupId, int DayOfWeek, TimeOnly StartTime, int DurationMinutes, int? ExcludeSlotId);

public sealed class GetScheduleConflictsHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly IClassGroupRepository _classGroups;
    private readonly ILogger<GetScheduleConflictsHandler> _logger;

    public GetScheduleConflictsHandler(IClassGroupScheduleRepository schedules, IClassGroupRepository classGroups,
        ILogger<GetScheduleConflictsHandler> logger)
    {
        _schedules = schedules;
        _classGroups = classGroups;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ScheduleConflictItem>>> ExecuteAsync(
        GetScheduleConflictsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var group = await _classGroups.GetByIdAsync(request.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult<IReadOnlyList<ScheduleConflictItem>>.Failure("الفوج غير موجود.", ErrorType.NotFound);

            // بلا قاعة وبلا أستاذ ← لا تعارض ممكناً
            IReadOnlyList<ScheduleConflictItem> conflicts = group.RoomId is null && group.TeacherId is null
                ? Array.Empty<ScheduleConflictItem>()
                : (await _schedules.FindConflictsAsync(request.DayOfWeek, request.StartTime.ToTimeSpan(), request.DurationMinutes,
                        group.RoomId, group.TeacherId, request.ExcludeSlotId, cancellationToken)).ToList();

            return OperationResult<IReadOnlyList<ScheduleConflictItem>>.Success(conflicts);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Schedule conflicts check cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check schedule conflicts for class group {ClassGroupId}", request.ClassGroupId);
            return OperationResult<IReadOnlyList<ScheduleConflictItem>>.Failure(
                "حدث خطأ غير متوقع أثناء فحص التعارض.", ErrorType.Unexpected);
        }
    }
}