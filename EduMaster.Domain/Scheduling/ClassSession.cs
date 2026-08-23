using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Scheduling
{
    /// <summary>
    /// حصة (D-90): مجدولة ← مُقامة (تفتح الحضور في 3.3) أو ملغاة (لا تخصم شيئاً) — لا حذف، التاريخ يُحفظ بالحالة.
    /// المصدر SourceScheduleId فارغ = حصة استثنائية (D-87) · الهوية الزمنية (الفوج + StartsAt) فريدة قاعدةً.
    /// StartsAt توقيت عمل محلي (ليس تدقيقاً) — حقول التدقيق تبقى UTC.
    /// </summary>
    public sealed class ClassSession
    {
        public int Id { get; private set; }
        public int ClassGroupId { get; private set; }
        public int? SourceScheduleId { get; private set; }
        public DateTime StartsAt { get; private set; }
        public int DurationMinutes { get; private set; }
        public SessionStatus Status { get; private set; }
        public string? Topic { get; private set; }
        public DateTime? CancelledAtUtc { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private ClassSession(int classGroupId, int? sourceScheduleId, DateTime startsAt, int durationMinutes,
            SessionStatus status, string? topic, DateTime? cancelledAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(classGroupId, sourceScheduleId, durationMinutes, topic);

            ClassGroupId = classGroupId;
            SourceScheduleId = sourceScheduleId;
            StartsAt = startsAt;
            DurationMinutes = durationMinutes;
            Status = status;
            Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
            CancelledAtUtc = cancelledAtUtc;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private ClassSession(int id, int classGroupId, int? sourceScheduleId, DateTime startsAt, int durationMinutes,
            SessionStatus status, string? topic, DateTime? cancelledAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(classGroupId, sourceScheduleId, startsAt, durationMinutes, status, topic, cancelledAtUtc,
                   createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static ClassSession Create(int classGroupId, int? sourceScheduleId, DateTime startsAt, int durationMinutes,
            string? topic, DateTime utcNow, int? createdByUserId)
        {
            return new ClassSession(classGroupId, sourceScheduleId, startsAt, durationMinutes,
                SessionStatus.Scheduled, topic, null, utcNow, createdByUserId, null, null);
        }

        public static ClassSession Load(int id, int classGroupId, int? sourceScheduleId, DateTime startsAt, int durationMinutes,
            SessionStatus status, string? topic, DateTime? cancelledAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new ClassSession(id, classGroupId, sourceScheduleId, startsAt, durationMinutes,
                status, topic, cancelledAtUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public bool IsScheduled => Status == SessionStatus.Scheduled;
        public bool IsHeld => Status == SessionStatus.Held;

        /// <summary>الإلغاء — المُقامة لا تُلغى (الحضور سُجّل فيها) · الملغاة سابقاً خاملة</summary>
        public void Cancel(DateTime utcNow, int? updatedByUserId)
        {
            if (Status == SessionStatus.Cancelled)
                return;
            if (Status == SessionStatus.Held)
                throw new DomainException("لا تُلغى حصة أُقيمت — الحضور سُجّل فيها.");

            Status = SessionStatus.Cancelled;
            CancelledAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>الإقامة تفتح الحضور (3.3) — الملغاة لا تُقام · المُقامة سابقاً خاملة</summary>
        public void MarkHeld(DateTime utcNow, int? updatedByUserId)
        {
            if (Status == SessionStatus.Held)
                return;
            if (Status == SessionStatus.Cancelled)
                throw new DomainException("حصة ملغاة لا تُقام — أنشئ حصة أخرى.");

            Status = SessionStatus.Held;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>الموضوع يُعدَّل ما دامت مجدولة فقط</summary>
        public void UpdateTopic(string? topic, DateTime utcNow, int? updatedByUserId)
        {
            if (Status != SessionStatus.Scheduled)
                throw new DomainException("موضوع الحصة يُعدَّل ما دامت مجدولة فقط.");

            Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(int classGroupId, int? sourceScheduleId, int durationMinutes, string? topic)
        {
            if (classGroupId <= 0)
                throw new DomainException("الحصة يجب أن تتبع فوجاً.");
            if (sourceScheduleId is <= 0)
                throw new DomainException("معرّف الموعد المصدر غير صالح.");
            if (durationMinutes <= 0 || durationMinutes > 600)
                throw new DomainException("مدة الحصة يجب أن تكون بين دقيقة و600 دقيقة.");
            if (topic is not null && topic.Trim().Length > 200)
                throw new DomainException("موضوع الحصة طويل جداً (الحد الأقصى 200 حرف).");
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