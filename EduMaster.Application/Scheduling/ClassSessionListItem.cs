using EduMaster.Domain.Enums;

namespace EduMaster.Application.Scheduling;

/// <summary>حصة مسطّحة لشاشة «الحصص» (D-40) — بعدد المسجَّلين النشطين (الحاضرون المتوقعون)</summary>
public sealed record ClassSessionListItem(
    int Id,
    int ClassGroupId,
    string GroupName,
    string SubjectName,
    string LevelName,
    string? TeacherFirstName,
    string? TeacherLastName,
    string? TeacherFatherName,
    string? RoomName,
    DateTime StartsAt,
    int DurationMinutes,
    SessionStatus Status,
    string? Topic,
    bool IsAdHoc,
    int ActiveEnrolledCount)
{
    public string? TeacherFullName => string.IsNullOrWhiteSpace(TeacherFirstName)
        ? null
        : string.Join(" ", new[] { TeacherFirstName, TeacherLastName, TeacherFatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public string StatusText => Status switch
    {
        SessionStatus.Scheduled => "مجدولة",
        SessionStatus.Held => "مُقامة",
        SessionStatus.Cancelled => "ملغاة",
        _ => "؟"
    };

    public string TimeDisplay => StartsAt.ToString("yyyy-MM-dd HH:mm");

    public string SourceText => IsAdHoc ? "استثنائية" : "من الجدول";
}