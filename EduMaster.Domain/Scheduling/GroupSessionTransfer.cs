using EduMaster.Domain.Common;

namespace EduMaster.Domain.Scheduling;

/// <summary>حركة نقل رصيد حصص بين تسجيلَي فوج — append-only ولا تغيّر الشراء أو الحضور التاريخيين.</summary>
public sealed class GroupSessionTransfer
{
    public int Id { get; private set; }
    public int FromClassGroupEnrollmentId { get; private set; }
    public int ToClassGroupEnrollmentId { get; private set; }
    public int SessionsCount { get; private set; }
    public DateTime TransferredAtUtc { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }

    private bool _idSet;

    private GroupSessionTransfer(int fromClassGroupEnrollmentId, int toClassGroupEnrollmentId,
        int sessionsCount, DateTime transferredAtUtc, string? note, DateTime createdAtUtc, int? createdByUserId)
    {
        Validate(fromClassGroupEnrollmentId, toClassGroupEnrollmentId, sessionsCount, note);
        FromClassGroupEnrollmentId = fromClassGroupEnrollmentId;
        ToClassGroupEnrollmentId = toClassGroupEnrollmentId;
        SessionsCount = sessionsCount;
        TransferredAtUtc = transferredAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    private GroupSessionTransfer(int id, int fromClassGroupEnrollmentId, int toClassGroupEnrollmentId,
        int sessionsCount, DateTime transferredAtUtc, string? note, DateTime createdAtUtc, int? createdByUserId)
        : this(fromClassGroupEnrollmentId, toClassGroupEnrollmentId, sessionsCount, transferredAtUtc, note, createdAtUtc, createdByUserId)
    {
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }

    public static GroupSessionTransfer Create(int fromClassGroupEnrollmentId, int toClassGroupEnrollmentId,
        int sessionsCount, string? note, DateTime utcNow, int? createdByUserId)
        => new(fromClassGroupEnrollmentId, toClassGroupEnrollmentId, sessionsCount, utcNow, note, utcNow, createdByUserId);

    public static GroupSessionTransfer Load(int id, int fromClassGroupEnrollmentId, int toClassGroupEnrollmentId,
        int sessionsCount, DateTime transferredAtUtc, string? note, DateTime createdAtUtc, int? createdByUserId)
        => new(id, fromClassGroupEnrollmentId, toClassGroupEnrollmentId, sessionsCount, transferredAtUtc, note, createdAtUtc, createdByUserId);

    private static void Validate(int fromClassGroupEnrollmentId, int toClassGroupEnrollmentId, int sessionsCount, string? note)
    {
        if (fromClassGroupEnrollmentId <= 0) throw new DomainException("مصدر نقل الرصيد غير صالح.");
        if (toClassGroupEnrollmentId <= 0) throw new DomainException("هدف نقل الرصيد غير صالح.");
        if (fromClassGroupEnrollmentId == toClassGroupEnrollmentId) throw new DomainException("لا يمكن نقل الرصيد إلى التسجيل نفسه.");
        if (sessionsCount <= 0) throw new DomainException("عدد الحصص المنقولة يجب أن يكون أكبر من صفر.");
        if (note is not null && note.Trim().Length > 200) throw new DomainException("ملاحظة نقل الرصيد طويلة جداً (الحد الأقصى 200 حرف).");
    }

    internal void SetId(int id)
    {
        if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
