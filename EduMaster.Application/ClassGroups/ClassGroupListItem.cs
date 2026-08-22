namespace EduMaster.Application.ClassGroups;

/// <summary>نموذج قراءة مسطّح لقائمة الأفواج (D-40) — يُملأ من JOIN مباشرة ولا يمر على الكيان الغني</summary>
public sealed record ClassGroupListItem(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    int LevelId,
    string LevelName,
    int SubjectId,
    string SubjectName,
    int? TeacherId,
    string? TeacherFirstName,
    string? TeacherLastName,
    string? TeacherFatherName,
    int? RoomId,
    string? RoomName,
    string Name,
    int? Capacity,
    string? StreamsText,
    bool IsActive)
{
    // الاسم ← اللقب ← اسم الأب (D-41)
    public string? TeacherFullName => TeacherId is null
        ? null
        : string.Join(" ", new[] { TeacherFirstName, TeacherLastName, TeacherFatherName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

    // فارغة = يقبل كل شعب المستوى (D-48)
    public string StreamsDisplay => string.IsNullOrWhiteSpace(StreamsText) ? "كل الشعب" : StreamsText;

    public string StatusText => IsActive ? "فعّال" : "معطّل";
}