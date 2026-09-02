namespace EduMaster.Application.Expenses;

public sealed record ExpenseCategoryItem(int Id, string Name, bool IsActive);

public sealed record ExpenseListItem(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    int ExpenseCategoryId,
    int TreasuryAccountId,
    string CategoryName,
    DateOnly ExpenseDate,
    long AmountCentimes,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
