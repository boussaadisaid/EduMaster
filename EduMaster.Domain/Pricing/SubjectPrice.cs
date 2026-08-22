using EduMaster.Domain.Common;

namespace EduMaster.Domain.Pricing
{
    /// <summary>
    /// سعر حصة مادة في مستوى لسنة معينة (D-49/D-50) — المصدر الوحيد للحقيقة السعرية.
    /// يُنسخ عند التسجيل (Snapshot في 2.4) ولا يُشار إليه — تعديله لا يمس النسخ القائمة.
    /// الهوية (سنة/مستوى/مادة) ثابتة: التعديل يمس السعر فقط، والخطأ في الهوية = حذف وإنشاء (D-65).
    /// صفر مسموح: مجاني صريح وواعٍ (D-65).
    /// </summary>
    public sealed class SubjectPrice
    {
        public int Id { get; private set; }
        public int AcademicYearId { get; private set; }
        public int LevelId { get; private set; }
        public int SubjectId { get; private set; }
        public long UnitPriceCentimes { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private SubjectPrice(int academicYearId, int levelId, int subjectId, long unitPriceCentimes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(academicYearId, levelId, subjectId, unitPriceCentimes);

            AcademicYearId = academicYearId;
            LevelId = levelId;
            SubjectId = subjectId;
            UnitPriceCentimes = unitPriceCentimes;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private SubjectPrice(int id, int academicYearId, int levelId, int subjectId, long unitPriceCentimes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(academicYearId, levelId, subjectId, unitPriceCentimes,
                   createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static SubjectPrice Create(int academicYearId, int levelId, int subjectId, long unitPriceCentimes,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new SubjectPrice(academicYearId, levelId, subjectId, unitPriceCentimes,
                createdAtUtc, createdByUserId, null, null);
        }

        public static SubjectPrice Load(int id, int academicYearId, int levelId, int subjectId, long unitPriceCentimes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new SubjectPrice(id, academicYearId, levelId, subjectId, unitPriceCentimes,
                createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        /// <summary>يعدّل السعر فقط — الهوية (السنة/المستوى/المادة) لا تُعدَّل بعد الإنشاء</summary>
        public void Update(long unitPriceCentimes, DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (unitPriceCentimes < 0)
                throw new DomainException("سعر الحصة لا يمكن أن يكون سالباً.");

            UnitPriceCentimes = unitPriceCentimes;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(int academicYearId, int levelId, int subjectId, long unitPriceCentimes)
        {
            if (academicYearId <= 0)
                throw new DomainException("السعر يجب أن يتبع سنة دراسية.");
            if (levelId <= 0)
                throw new DomainException("السعر يجب أن يتبع مستوى.");
            if (subjectId <= 0)
                throw new DomainException("السعر يجب أن يتبع مادة.");
            if (unitPriceCentimes < 0)
                throw new DomainException("سعر الحصة لا يمكن أن يكون سالباً.");
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