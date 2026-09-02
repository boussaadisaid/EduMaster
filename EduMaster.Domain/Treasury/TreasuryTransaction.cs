using EduMaster.Domain.Common;

namespace EduMaster.Domain.Treasury;

public sealed class TreasuryTransaction
{
    private bool _idSet;

    public int Id { get; private set; }
    public int TreasuryAccountId { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public TreasuryTransactionKind Kind { get; private set; }
    public long AmountCentimes { get; private set; }
    public string? Note { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public int? UpdatedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public int? DeletedByUserId { get; private set; }

    private TreasuryTransaction(int treasuryAccountId, DateOnly transactionDate, TreasuryTransactionKind kind,
        long amountCentimes, string? note, bool isDeleted, DateTime createdAtUtc, int? createdByUserId,
        DateTime? updatedAtUtc, int? updatedByUserId, DateTime? deletedAtUtc, int? deletedByUserId)
    {
        Validate(treasuryAccountId, kind, amountCentimes, note);
        TreasuryAccountId = treasuryAccountId;
        TransactionDate = transactionDate;
        Kind = kind;
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

    public static TreasuryTransaction Create(int treasuryAccountId, DateOnly transactionDate, TreasuryTransactionKind kind,
        long amountCentimes, string? note, DateTime createdAtUtc, int? createdByUserId)
        => new(treasuryAccountId, transactionDate, kind, amountCentimes, note, false,
            createdAtUtc, createdByUserId, null, null, null, null);

    public static TreasuryTransaction Load(int id, int treasuryAccountId, DateOnly transactionDate, TreasuryTransactionKind kind,
        long amountCentimes, string? note, bool isDeleted, DateTime createdAtUtc, int? createdByUserId,
        DateTime? updatedAtUtc, int? updatedByUserId, DateTime? deletedAtUtc, int? deletedByUserId)
    {
        if (id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر.");
        var item = new TreasuryTransaction(treasuryAccountId, transactionDate, kind, amountCentimes, note, isDeleted,
            createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId, deletedAtUtc, deletedByUserId);
        item.Id = id;
        item._idSet = true;
        return item;
    }

    public void Update(int treasuryAccountId, DateOnly transactionDate, TreasuryTransactionKind kind,
        long amountCentimes, string? note, DateTime updatedAtUtc, int? updatedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("لا يمكن تعديل حركة محذوفة.");
        Validate(treasuryAccountId, kind, amountCentimes, note);
        TreasuryAccountId = treasuryAccountId;
        TransactionDate = transactionDate;
        Kind = kind;
        AmountCentimes = amountCentimes;
        Note = NormalizeNote(note);
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void SoftDelete(DateTime deletedAtUtc, int? deletedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("الحركة محذوفة مسبقاً.");
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
    }

    private static void Validate(int treasuryAccountId, TreasuryTransactionKind kind, long amountCentimes, string? note)
    {
        if (treasuryAccountId <= 0)
            throw new DomainException("الحركة يجب أن ترتبط بحساب مالي.");
        if (!Enum.IsDefined(kind))
            throw new DomainException("نوع الحركة المالية غير صالح.");
        if (amountCentimes <= 0)
            throw new DomainException("مبلغ الحركة يجب أن يكون أكبر من صفر.");
        if (note is not null && note.Trim().Length > 500)
            throw new DomainException("الملاحظة طويلة جداً (الحد الأقصى 500 حرف).");
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    internal void SetId(int id)
    {
        if (_idSet)
            throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه.");
        if (id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر.");
        Id = id;
        _idSet = true;
    }
}
