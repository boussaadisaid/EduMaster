using EduMaster.Domain.Enums;

namespace EduMaster.Application.Employees;

/// <summary>صف قراءة مسطّح لموظف (D-40) — الاسم والهاتف والتفعيل من نواة الشخص</summary>
public sealed record EmployeeListItem(
    int Id,                     // معرف ملف الموظف (لا الشخص)
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
    string JobTitle,
    string? Notes,
    bool IsActive)
{
    // الاسم ← اللقب ← اسم الأب (D-41)
    public string FullName =>
        string.Join(" ", new[] { FirstName, LastName, FatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));
}