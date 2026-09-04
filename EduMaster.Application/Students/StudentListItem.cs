using EduMaster.Domain.Enums;
using EduMaster.Domain.Students;

namespace EduMaster.Application.Students;

public sealed record StudentListItem(
    int Id,                     // معرف ملف الطالب (لا الشخص)
    int PersonId,
    string FirstName,
    string LastName,
    string? FatherName,
    DateOnly? BirthDate,
    GenderType? Gender,
    string? Phone,
    string? Phone2,
    string? Email,
    string? Address,
    string? PhotoPath,
    StudentCategory Category,
    int? GuardianPersonId,
    string? GuardianFullName,
    string? GuardianPhone,
    string? Notes,
    bool IsActive)
{
    // الاسم ← اللقب ← اسم الأب (الفجوة المصلحة)
    public string FullName =>
        string.Join(" ", new[] { FirstName, LastName, FatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public string CategoryText => Category switch
    {
        StudentCategory.Regular => "نظامي",
        StudentCategory.FreeCandidate => "مترشح حر",
        StudentCategory.University => "جامعي",
        StudentCategory.Training => "تكوين ودورات",
        _ => "—"
    };
}