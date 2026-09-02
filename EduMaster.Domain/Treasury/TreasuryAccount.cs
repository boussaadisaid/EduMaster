using EduMaster.Domain.Common;

namespace EduMaster.Domain.Treasury;

public sealed class TreasuryAccount
{
    private bool _idSet;

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public long OpeningBalanceCentimes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public int? UpdatedByUserId { get; private set; }

    private TreasuryAccount(string name, bool isActive, long openingBalanceCentimes,
        DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
    {
        Validate(name, openingBalanceCentimes);
        Name = name.Trim();
        IsActive = isActive;
        OpeningBalanceCentimes = openingBalanceCentimes;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    private TreasuryAccount(int id, string name, bool isActive, long openingBalanceCentimes,
        DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        : this(name, isActive, openingBalanceCentimes, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
    {
        if (id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر.");
        Id = id;
        _idSet = true;
    }

    public static TreasuryAccount Create(string name, long openingBalanceCentimes, DateTime createdAtUtc, int? createdByUserId)
        => new(name, true, openingBalanceCentimes, createdAtUtc, createdByUserId, null, null);

    public static TreasuryAccount Load(int id, string name, bool isActive, long openingBalanceCentimes,
        DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        => new(id, name, isActive, openingBalanceCentimes, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);

    public void Update(string name, long openingBalanceCentimes, DateTime updatedAtUtc, int? updatedByUserId)
    {
        Validate(name, openingBalanceCentimes);
        Name = name.Trim();
        OpeningBalanceCentimes = openingBalanceCentimes;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void Activate(DateTime updatedAtUtc, int? updatedByUserId)
    {
        IsActive = true;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void Deactivate(DateTime updatedAtUtc, int? updatedByUserId)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    private static void Validate(string? name, long openingBalanceCentimes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("أدخل اسم الحساب المالي.");
        if (name.Trim().Length > 100)
            throw new DomainException("اسم الحساب طويل جداً (الحد الأقصى 100 حرف).");
        if (openingBalanceCentimes < 0)
            throw new DomainException("الرصيد الافتتاحي لا يمكن أن يكون سالباً.");
    }

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
