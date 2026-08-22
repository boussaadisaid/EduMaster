using EduMaster.Domain.Common;

namespace EduMaster.Domain.ClassGroups
{
    /// <summary>
    /// الفوج الدراسي — فوج مادة سنوي (D-47): سنة + مستوى + مادة + أستاذ اختياري.
    /// الهوية (السنة/المستوى/المادة) ثابتة بعد الإنشاء — التغيير الجوهري = فوج جديد وتعطيل القديم.
    /// شعب الفوج لا يحملها الكيان — قائمة M:N تُدار عبر الـRepository في نفس المعاملة (D-48).
    /// القاعة والسعة اختياريتان دائماً (D-44) — والسعة يحرسها التسجيل في 2.4.
    /// </summary>
    public sealed class ClassGroup
    {
        public int Id { get; private set; }
        public int AcademicYearId { get; private set; }
        public int LevelId { get; private set; }
        public int SubjectId { get; private set; }
        public int? TeacherId { get; private set; }
        public int? RoomId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string NameNormalized { get; private set; } = string.Empty;
        public int? Capacity { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private ClassGroup(int academicYearId, int levelId, int subjectId, int? teacherId, int? roomId,
            string name, int? capacity, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(academicYearId, levelId, subjectId, teacherId, name, capacity);

            AcademicYearId = academicYearId;
            LevelId = levelId;
            SubjectId = subjectId;
            TeacherId = teacherId;
            RoomId = roomId;
            Name = name.Trim();
            NameNormalized = ArabicTextNormalizer.Normalize(name);
            Capacity = capacity;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private ClassGroup(int id, int academicYearId, int levelId, int subjectId, int? teacherId, int? roomId,
            string name, int? capacity, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(academicYearId, levelId, subjectId, teacherId, roomId, name, capacity, isActive,
                   createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static ClassGroup Create(int academicYearId, int levelId, int subjectId,
            int? teacherId, int? roomId, string name, int? capacity,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new ClassGroup(academicYearId, levelId, subjectId, teacherId, roomId, name, capacity, true,
                createdAtUtc, createdByUserId, null, null);
        }

        public static ClassGroup Load(int id, int academicYearId, int levelId, int subjectId,
            int? teacherId, int? roomId, string name, int? capacity, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new ClassGroup(id, academicYearId, levelId, subjectId, teacherId, roomId, name, capacity, isActive,
                createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        /// <summary>يعدّل الاسم والقاعة والسعة فقط — الهوية (السنة/المستوى/المادة) لا تُعدَّل بعد الإنشاء</summary>
        public void Update(string name, int? roomId, int? capacity, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(AcademicYearId, LevelId, SubjectId, TeacherId, name, capacity);

            Name = name.Trim();
            NameNormalized = ArabicTextNormalizer.Normalize(name);
            RoomId = roomId;
            Capacity = capacity;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>إسناد أستاذ أو تغييره أو سحبه (null) — التحقق من صلاحية الأستاذ في الـHandler</summary>
        public void AssignTeacher(int? teacherId, DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (teacherId is <= 0)
                throw new DomainException("معرّف الأستاذ غير صالح.");

            TeacherId = teacherId;
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

        private static void Validate(int academicYearId, int levelId, int subjectId, int? teacherId,
            string? name, int? capacity)
        {
            if (academicYearId <= 0)
                throw new DomainException("الفوج يجب أن يتبع سنة دراسية.");
            if (levelId <= 0)
                throw new DomainException("الفوج يجب أن يتبع مستوى.");
            if (subjectId <= 0)
                throw new DomainException("الفوج يجب أن يتبع مادة.");
            if (teacherId is <= 0)
                throw new DomainException("معرّف الأستاذ غير صالح.");
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("أدخل اسم الفوج.");
            if (name.Trim().Length > 100)
                throw new DomainException("اسم الفوج طويل جداً (الحد الأقصى 100 حرف).");
            if (capacity is <= 0)
                throw new DomainException("سعة الفوج يجب أن تكون أكبر من صفر أو تُترك فارغة.");
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