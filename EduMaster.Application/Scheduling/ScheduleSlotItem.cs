using EduMaster.Domain.Common;

namespace EduMaster.Application.Scheduling;

/// <summary>موعد أسبوعي مسطّح بأسماء الفوج/المادة/المستوى/الأستاذ/القاعة (D-40) — يغذي شاشة الجدول والتوليد</summary>
public sealed record ScheduleSlotItem(
    int Id,
    int ClassGroupId,
    string GroupName,
    string SubjectName,
    string LevelName,
    string? TeacherFirstName,
    string? TeacherLastName,
    string? TeacherFatherName,
    string? RoomName,
    int DayOfWeek,
    TimeOnly StartTime,
    int DurationMinutes,
    bool IsActive)
{
    public string DayName => SchoolWeek.ArabicName(DayOfWeek);

    public string TimeDisplay => $"{StartTime.ToString("HH:mm")} — {DurationMinutes} د";

    public TimeOnly EndTime => StartTime.AddMinutes(DurationMinutes);

    // الاسم ← اللقب ← اسم الأب (D-41)
    public string? TeacherFullName => string.IsNullOrWhiteSpace(TeacherFirstName)
        ? null
        : string.Join(" ", new[] { TeacherFirstName, TeacherLastName, TeacherFatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));
}