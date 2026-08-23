using EduMaster.Domain.Common;

namespace EduMaster.Domain.Scheduling
{
    /// <summary>
    /// موعد أسبوعي في جدول استعمال الزمن (D-86): قالب توليد — عدة مواعيد للفوج الواحد.
    /// تعديله/تعطيله يلغي حصصه المستقبلية المجدولة عبر الـHandler (D-88) — والمولَّد سابقاً ملك نفسه.
    /// </summary>
    public sealed class ClassGroupSchedule
    {
        public int Id { get; private set; }
        public int ClassGroupId { get; private set; }
        public int DayOfWeek { get; private set; }          // 1=السبت … 7=الجمعة (SchoolWeek)
        public TimeOnly StartTime { get; private set; }
        public int DurationMinutes { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private ClassGroupSchedule(int classGroupId, int dayOfWeek, TimeOnly startTime, int durationMinutes,
            bool isActive, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(classGroupId, dayOfWeek, durationMinutes);

            ClassGroupId = classGroupId;
            DayOfWeek = dayOfWeek;
            StartTime = startTime;
            DurationMinutes = durationMinutes;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private ClassGroupSchedule(int id, int classGroupId, int dayOfWeek, TimeOnly startTime, int durationMinutes,
            bool isActive, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(classGroupId, dayOfWeek, startTime, durationMinutes, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static ClassGroupSchedule Create(int classGroupId, int dayOfWeek, TimeOnly startTime, int durationMinutes,
            DateTime utcNow, int? createdByUserId)
        {
            return new ClassGroupSchedule(classGroupId, dayOfWeek, startTime, durationMinutes,
                true, utcNow, createdByUserId, null, null);
        }

        public static ClassGroupSchedule Load(int id, int classGroupId, int dayOfWeek, TimeOnly startTime, int durationMinutes,
            bool isActive, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new ClassGroupSchedule(id, classGroupId, dayOfWeek, startTime, durationMinutes,
                isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        /// <summary>تعديل الموعد — كاسكيد إلغاء الحصص المستقبلية في الـHandler (D-88)</summary>
        public void Update(int dayOfWeek, TimeOnly startTime, int durationMinutes, DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (!IsActive)
                throw new DomainException("موعد معطّل لا يُعدَّل — فعّله أولاً.");
            Validate(ClassGroupId, dayOfWeek, durationMinutes);

            DayOfWeek = dayOfWeek;
            StartTime = startTime;
            DurationMinutes = durationMinutes;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>تعطيل الموعد — كاسكيد D-88 في الـHandler</summary>
        public void Deactivate(DateTime utcNow, int? updatedByUserId)
        {
            if (!IsActive) return;
            IsActive = false;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        public void Activate(DateTime utcNow, int? updatedByUserId)
        {
            if (IsActive) return;
            IsActive = true;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(int classGroupId, int dayOfWeek, int durationMinutes)
        {
            if (classGroupId <= 0)
                throw new DomainException("الموعد يجب أن يتبع فوجاً.");
            if (dayOfWeek < 1 || dayOfWeek > 7)
                throw new DomainException("يوم الأسبوع غير صالح (1=السبت … 7=الجمعة).");
            if (durationMinutes <= 0 || durationMinutes > 600)
                throw new DomainException("مدة الحصة يجب أن تكون بين دقيقة و600 دقيقة.");
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