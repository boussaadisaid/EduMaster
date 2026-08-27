using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Reports;

/// <summary>
/// حضور الطلاب لفترة (6.4 — ق-1): علامات خام ← تجميع (طالب × فوج) بأكثر الغائبين أولاً — هم موضوع المكتب ·
/// قراءة خالصة بلا معاملة وترمي الإلغاء (D-64) · المبرر يُحسب ولا يدخل النسبة (اتساق D-93)
/// </summary>
public sealed class GetAttendanceSummaryHandler
{
    private readonly IReportRepository _reports;
    private readonly ILogger<GetAttendanceSummaryHandler> _logger;

    public GetAttendanceSummaryHandler(IReportRepository reports, ILogger<GetAttendanceSummaryHandler> logger)
    {
        _reports = reports;
        _logger = logger;
    }

    public async Task<OperationResult<AttendanceSummaryReportItem>> ExecuteAsync(
        DateOnly from, DateOnly to, int? classGroupId, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return OperationResult<AttendanceSummaryReportItem>.Failure("تاريخ «من» لا يمكن أن يكون بعد «إلى».", ErrorType.Validation);

        try
        {
            // الحدود: DateTime بتوقيت العمل المحلي — من بداية «من» إلى بداية ما بعد «إلى» (نمط GetSessionsHandler القائم)
            var marks = await _reports.GetAttendanceMarksForPeriodAsync(
                from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue).AddDays(1),
                classGroupId, cancellationToken);

            var rows = marks
                .GroupBy(m => (m.StudentId, m.ClassGroupId))
                .Select(g =>
                {
                    var first = g.First();
                    return new AttendanceSummaryItem(
                        first.StudentId, first.StudentName, first.ClassGroupId, first.GroupName,
                        g.Count(m => m.Status == AttendanceStatus.Present),
                        g.Count(m => m.Status == AttendanceStatus.Absent),
                        g.Count(m => m.Status == AttendanceStatus.Justified));
                })
                .OrderByDescending(r => r.AbsentCount)   // الأكثر غياباً أولاً — هم سؤال المكتب اليومي
                .ThenBy(r => r.StudentName)
                .ToList();

            return OperationResult<AttendanceSummaryReportItem>.Success(new AttendanceSummaryReportItem(from, to, rows));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build attendance summary from {From} to {To}", from, to);
            return OperationResult<AttendanceSummaryReportItem>.Failure("حدث خطأ غير متوقع أثناء إعداد تقرير الحضور.", ErrorType.Unexpected);
        }
    }
}
