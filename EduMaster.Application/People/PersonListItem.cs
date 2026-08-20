using EduMaster.Domain.Enums;


public sealed record PersonListItem(
    int Id,
    string FirstName,
    string LastName,
    string? FatherName,
    DateOnly? BirthDate,
    GenderType? Gender,
    string? Phone,
    string? Phone2,
    string? Email,
    string? Address,
    bool IsActive)
{
    /// <summary>الاسم الثلاثي للعرض — بلا مسافات زائدة حتى مع غياب اسم الأب</summary>
    public string FullName =>
        string.Join(" ", new[] { FirstName, FatherName, LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
}