using EduMaster.Domain.Common;

namespace EduMaster.Domain.Expenses;

public sealed class Expense
{
    private bool _idSet;

    public int Id { get; private set; }
    public int AcademicYearId { get; private set; }
    public int ExpenseCategoryId { get; private set; }
    public DateOnly ExpenseDate { get; private set; }
    public long AmountCentimes { get; private set; }
    public string? Note { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public int? UpdatedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public int? DeletedByUserId { get; private set; }

    private Expense(int academicYearId, int expenseCategoryId, DateOnly expenseDate, long amountCentimes, string? note,
        bool isDeleted, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId,
        DateTime? deletedAtUtc, int? deletedByUserId)
    {
        Validate(academicYearId, expenseCategoryId, amountCentimes, note);
        AcademicYearId = academicYearId;
        ExpenseCategoryId = expenseCategoryId;
        ExpenseDate = expenseDate;
        AmountCentimes = amountCentimes;
        Note = NormalizeNote(note);
        IsDeleted = isDeleted;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
    }

    public static Expense Create(int academicYearId, int expenseCategoryId, DateOnly expenseDate,
        long amountCentimes, string? note, DateTime createdAtUtc, int? createdByUserId)
        => new(academicYearId, expenseCategoryId, expenseDate, amountCentimes, note, false,
            createdAtUtc, createdByUserId, null, null, null, null);

    public static Expense Load(int id, int academicYearId, int expenseCategoryId, DateOnly expenseDate,
        long amountCentimes, string? note, bool isDeleted, DateTime createdAtUtc, int? createdByUserId,
        DateTime? updatedAtUtc, int? updatedByUserId, DateTime? deletedAtUtc, int? deletedByUserId)
    {
        var expense = new Expense(academicYearId, expenseCategoryId, expenseDate, amountCentimes, note, isDeleted,
            createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId, deletedAtUtc, deletedByUserId);
        expense.Id = id;
        expense._idSet = true;
        return expense;
    }

    public void Update(int academicYearId, int expenseCategoryId, DateOnly expenseDate, long amountCentimes,
        string? note, DateTime updatedAtUtc, int? updatedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("لا يمكن تعديل مصروف محذوف.");

        Validate(academicYearId, expenseCategoryId, amountCentimes, note);
        AcademicYearId = academicYearId;
        ExpenseCategoryId = expenseCategoryId;
        ExpenseDate = expenseDate;
        AmountCentimes = amountCentimes;
        Note = NormalizeNote(note);
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void SoftDelete(DateTime deletedAtUtc, int? deletedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("المصروف محذوف مسبقاً.");

        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
    }

    private static void Validate(int academicYearId, int expenseCategoryId, long amountCentimes, string? note)
    {
        if (academicYearId <= 0)
            throw new DomainException("المصروف يجب أن يرتبط بسنة دراسية.");
        if (expenseCategoryId <= 0)
            throw new DomainException("المصروف يجب أن يرتبط بفئة.");
        if (amountCentimes <= 0)
            throw new DomainException("مبلغ المصروف يجب أن يكون أكبر من صفر.");
        if (note is not null && note.Trim().Length > 500)
            throw new DomainException("الملاحظة طويلة جداً (الحد الأقصى 500 حرف).");
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    internal void SetId(int id)
    {
        if (_idSet || id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
