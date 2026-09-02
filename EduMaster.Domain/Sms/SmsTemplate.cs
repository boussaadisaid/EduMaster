using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Sms;

public sealed class SmsTemplate
{
    private bool _idSet;

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SmsMessageCategory Category { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public int? UpdatedByUserId { get; private set; }

    private SmsTemplate(string name, SmsMessageCategory category, string body, bool isActive,
        DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
    {
        Validate(name, body);
        if (!Enum.IsDefined(category)) throw new DomainException("نوع قالب الرسالة غير صالح.");
        Name = name.Trim();
        Category = category;
        Body = body.Trim();
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    private SmsTemplate(int id, string name, SmsMessageCategory category, string body, bool isActive,
        DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        : this(name, category, body, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
    {
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }

    public static SmsTemplate Create(string name, SmsMessageCategory category, string body,
        DateTime createdAtUtc, int? createdByUserId)
        => new(name, category, body, true, createdAtUtc, createdByUserId, null, null);

    public static SmsTemplate Load(int id, string name, SmsMessageCategory category, string body, bool isActive,
        DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        => new(id, name, category, body, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);

    public void Update(string name, SmsMessageCategory category, string body, DateTime updatedAtUtc, int? updatedByUserId)
    {
        Validate(name, body);
        if (!Enum.IsDefined(category)) throw new DomainException("نوع قالب الرسالة غير صالح.");
        Name = name.Trim();
        Category = category;
        Body = body.Trim();
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

    private static void Validate(string? name, string? body)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("اسم القالب مطلوب.");
        if (name.Trim().Length > 100) throw new DomainException("اسم القالب طويل جداً (الحد الأقصى 100 حرف).");
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("نص القالب مطلوب.");
        if (body.Trim().Length > 1000) throw new DomainException("نص القالب طويل جداً (الحد الأقصى 1000 حرف).");
    }

    internal void SetId(int id)
    {
        if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
