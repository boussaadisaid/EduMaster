using EduMaster.Domain.Common;

namespace EduMaster.Domain.Scheduling
{
    /// <summary>
    /// شراء حصص على تسجيل فوج (D-91): append-only — كل شراء صف جديد (تاريخ شراء نظيف).
    /// كمية فقط بلا مبلغ (D-96) — ثمن الحزمة = عدد × AgreedUnitPriceCentimes المسنابشوت على التسجيل.
    /// الرصيد = Σمشتريات − Σمخصوم (المخصوم من الحضور — 3.3).
    /// </summary>
    public sealed class GroupSessionPurchase
    {
        public int Id { get; private set; }
        public int ClassGroupEnrollmentId { get; private set; }
        public int SessionsCount { get; private set; }
        public DateTime PurchasedAtUtc { get; private set; }
        public string? Note { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private GroupSessionPurchase(int classGroupEnrollmentId, int sessionsCount, DateTime purchasedAtUtc, string? note,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(classGroupEnrollmentId, sessionsCount, note);

            ClassGroupEnrollmentId = classGroupEnrollmentId;
            SessionsCount = sessionsCount;
            PurchasedAtUtc = purchasedAtUtc;
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private GroupSessionPurchase(int id, int classGroupEnrollmentId, int sessionsCount, DateTime purchasedAtUtc, string? note,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(classGroupEnrollmentId, sessionsCount, purchasedAtUtc, note, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static GroupSessionPurchase Create(int classGroupEnrollmentId, int sessionsCount, string? note,
            DateTime utcNow, int? createdByUserId)
        {
            return new GroupSessionPurchase(classGroupEnrollmentId, sessionsCount, utcNow, note,
                utcNow, createdByUserId, null, null);
        }

        public static GroupSessionPurchase Load(int id, int classGroupEnrollmentId, int sessionsCount, DateTime purchasedAtUtc, string? note,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new GroupSessionPurchase(id, classGroupEnrollmentId, sessionsCount, purchasedAtUtc, note,
                createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        private static void Validate(int classGroupEnrollmentId, int sessionsCount, string? note)
        {
            if (classGroupEnrollmentId <= 0)
                throw new DomainException("المشتراة يجب أن تتبع تسجيل فوج.");
            if (sessionsCount <= 0)
                throw new DomainException("عدد الحصص المشتراة يجب أن يكون أكبر من صفر.");
            if (note is not null && note.Trim().Length > 200)
                throw new DomainException("الملاحظة طويلة جداً (الحد الأقصى 200 حرف).");
        }

        internal void SetId(int id)
        {
            if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
            if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }
    }
}