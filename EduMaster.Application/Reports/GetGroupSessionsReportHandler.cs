using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Reports;

/// <summary>
/// حصص الأفواج لفترة (6.4 — ق-2): يلفّ قراءة الحصص القائمة بتجميع وإجماليات مشتقة — بلا SQL جديد (روح 6.1/D-109) ·
/// قراءة خالصة بلا معاملة وترمي الإلغاء (D-64) · دقائق المُقامة تخدم مراقبة أجور «بالساعة» (روح D-124)
/// </summary>
public sealed class GetGroupSessionsReportHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly ILogger<GetGroupSessionsReportHandler> _logger;

    public GetGroupSessionsReportHandler(IClassSessionRepository sessions, ILogger<GetGroupSessionsReportHandler> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    public async Task<OperationResult<GroupSessionsReportItem>> ExecuteAsync(
        DateOnly from, DateOnly to, int? classGroupId, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return OperationResult<GroupSessionsReportItem>.Failure("تاريخ «من» لا يمكن أن يكون بعد «إلى».", ErrorType.Validation);

        try
        {
            var sessions = await _sessions.GetByDateRangeAsync(
                from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue).AddDays(1),
                classGroupId, cancellationToken);

            var groups = sessions
                .GroupBy(s => s.ClassGroupId)
                .Select(g =>
                {
                    var first = g.First();
                    return new GroupSessionsSummaryItem(
                        g.Key, first.GroupName, first.SubjectName, first.LevelName, first.TeacherFullName,
                        g.Count(s => s.Status == SessionStatus.Scheduled),
                        g.Count(s => s.Status == SessionStatus.Held),
                        g.Count(s => s.Status == SessionStatus.Cancelled),
                        g.Where(s => s.Status == SessionStatus.Held).Sum(s => s.DurationMinutes));   // الملغاة لا دقائق لها (D-90)
                })
                .OrderBy(r => r.LevelName)
                .ThenBy(r => r.GroupName)
                .ToList();

            return OperationResult<GroupSessionsReportItem>.Success(new GroupSessionsReportItem(from, to, groups));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build group sessions report from {From} to {To}", from, to);
            return OperationResult<GroupSessionsReportItem>.Failure("حدث خطأ غير متوقع أثناء إعداد تقرير الحصص.", ErrorType.Unexpected);
        }
    }
}
