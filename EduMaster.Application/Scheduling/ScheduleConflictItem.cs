using EduMaster.Domain.Common;

namespace EduMaster.Application.Scheduling;

/// <summary>تعارض موعد (D-89 — تحذير غير مانع): Reason = «القاعة» أو «الأستاذ» أو «القاعة والأستاذ»</summary>
public sealed record ScheduleConflictItem(
    string GroupName,
    string SubjectName,
    int DayOfWeek,
    TimeOnly StartTime,
    int DurationMinutes,
    string? RoomName,
    string? TeacherFullName,
    string Reason)
{
    public string DayName => SchoolWeek.ArabicName(DayOfWeek);

    public string TimeDisplay => StartTime.ToString("HH:mm");
}