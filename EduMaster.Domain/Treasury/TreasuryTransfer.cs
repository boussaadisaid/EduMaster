using EduMaster.Domain.Common;

namespace EduMaster.Domain.Treasury;

public sealed class TreasuryTransfer
{
    private bool _idSet;

    public int Id { get; private set; }
    public int FromTreasuryAccountId { get; private set; }
    public int ToTreasuryAccountId { get; private set; }
    public DateOnly TransferDate { get; private set; }
    public long AmountCentimes { get; private set; }
    public string? Note { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public int? DeletedByUserId { get; private set; }

    private TreasuryTransfer(int fromTreasuryAccountId, int toTreasuryAccountId, DateOnly transferDate,
        long amountCentimes, string? note, bool isDeleted, DateTime createdAtUtc, int? createdByUserId,
        DateTime? deletedAtUtc, int? deletedByUserId)
    {
        Validate(fromTreasuryAccountId, toTreasuryAccountId, amountCentimes, note);
        FromTreasuryAccountId = fromTreasuryAccountId;
        ToTreasuryAccountId = toTreasuryAccountId;
        TransferDate = transferDate;
        AmountCentimes = amountCentimes;
        Note = NormalizeNote(note);
        IsDeleted = isDeleted;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
    }

    public static TreasuryTransfer Create(int fromTreasuryAccountId, int toTreasuryAccountId, DateOnly transferDate,
        long amountCentimes, string? note, DateTime createdAtUtc, int? createdByUserId)
        => new(fromTreasuryAccountId, toTreasuryAccountId, transferDate, amountCentimes, note, false,
            createdAtUtc, createdByUserId, null, null);

    public static TreasuryTransfer Load(int id, int fromTreasuryAccountId, int toTreasuryAccountId, DateOnly transferDate,
        long amountCentimes, string? note, bool isDeleted, DateTime createdAtUtc, int? createdByUserId,
        DateTime? deletedAtUtc, int? deletedByUserId)
    {
        if (id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر.");
        var item = new TreasuryTransfer(fromTreasuryAccountId, toTreasuryAccountId, transferDate, amountCentimes, note,
            isDeleted, createdAtUtc, createdByUserId, deletedAtUtc, deletedByUserId);
        item.Id = id;
        item._idSet = true;
        return item;
    }

    public void SoftDelete(DateTime deletedAtUtc, int? deletedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("التحويل محذوف مسبقاً.");
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
    }

    private static void Validate(int fromTreasuryAccountId, int toTreasuryAccountId, long amountCentimes, string? note)
    {
        if (fromTreasuryAccountId <= 0 || toTreasuryAccountId <= 0)
            throw new DomainException("التحويل يجب أن يرتبط بحسابين صالحين.");
        if (fromTreasuryAccountId == toTreasuryAccountId)
            throw new DomainException("لا يمكن تحويل المال إلى الحساب نفسه.");
        if (amountCentimes <= 0)
            throw new DomainException("مبلغ التحويل يجب أن يكون أكبر من صفر.");
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
