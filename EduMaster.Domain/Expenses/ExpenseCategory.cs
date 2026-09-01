using EduMaster.Domain.Common;

namespace EduMaster.Domain.Expenses;

public sealed class ExpenseCategory
{
    private bool _idSet;

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public int? UpdatedByUserId { get; private set; }

    private ExpenseCategory(string name, bool isActive, DateTime createdAtUtc, int? createdByUserId,
        DateTime? updatedAtUtc, int? updatedByUserId)
    {
        Validate(name);
        Name = name.Trim();
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    private ExpenseCategory(int id, string name, bool isActive, DateTime createdAtUtc, int? createdByUserId,
        DateTime? updatedAtUtc, int? updatedByUserId)
        : this(name, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
    {
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }

    public static ExpenseCategory Create(string name, DateTime createdAtUtc, int? createdByUserId)
        => new(name, true, createdAtUtc, createdByUserId, null, null);

    public static ExpenseCategory Load(int id, string name, bool isActive, DateTime createdAtUtc,
        int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        => new(id, name, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);

    public void Update(string name, DateTime updatedAtUtc, int? updatedByUserId)
    {
        Validate(name);
        Name = name.Trim();
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void Deactivate(DateTime updatedAtUtc, int? updatedByUserId)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void Activate(DateTime updatedAtUtc, int? updatedByUserId)
    {
        IsActive = true;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    private static void Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("أدخل اسم فئة المصروف.");
        if (name.Trim().Length > 50)
            throw new DomainException("اسم الفئة طويل جداً (الحد الأقصى 50 حرفاً).");
    }

    internal void SetId(int id)
    {
        if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
