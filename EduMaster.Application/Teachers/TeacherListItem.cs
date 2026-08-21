using EduMaster.Domain.Enums;

namespace EduMaster.Application.Teachers;

public sealed record TeacherListItem(
    int Id,                     // معرف ملف الأستاذ (لا الشخص)
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
    string? Specialty,
    string? Notes,
    bool IsActive)
{
    public string FullName =>
        string.Join(" ", new[] { FirstName, LastName, FatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));
}