using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Enrollments
{
    /// <summary>
    /// تسجيل طالب في فوج (D-47) فوق تسجيله السنوي المطابق (D-54) — بأسعار Snapshot (D-03/D-52/D-77).
    /// الحالة نشط/منسحب فقط (D-53) — النقل عملية (انسحاب + إلحاق بمعاملة واحدة — D-78) والعودة بصف جديد.
    /// الأسعار ثابتة بعد الإلحاق في 2.4 — تعديلها اللاحق يُحسم في F4 مع القبض.
    /// </summary>
    public sealed class ClassGroupEnrollment
    {
        public int Id { get; private set; }
        public int ClassGroupId { get; private set; }
        public int StudentId { get; private set; }
        public int AnnualEnrollmentId { get; private set; }
        public EnrollmentStatus Status { get; private set; }
        public long SnapshotUnitPriceCentimes { get; private set; }
        public long AgreedUnitPriceCentimes { get; private set; }
        public string? DiscountNote { get; private set; }
        public DateTime EnrolledAtUtc { get; private set; }
        public DateTime? WithdrawnAtUtc { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private ClassGroupEnrollment(int classGroupId, int studentId, int annualEnrollmentId,
            EnrollmentStatus status, long snapshotUnitPriceCentimes, long agreedUnitPriceCentimes, string? discountNote,
            DateTime enrolledAtUtc, DateTime? withdrawnAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(classGroupId, studentId, annualEnrollmentId, snapshotUnitPriceCentimes, agreedUnitPriceCentimes, discountNote);

            ClassGroupId = classGroupId;
            StudentId = studentId;
            AnnualEnrollmentId = annualEnrollmentId;
            Status = status;
            SnapshotUnitPriceCentimes = snapshotUnitPriceCentimes;
            AgreedUnitPriceCentimes = agreedUnitPriceCentimes;
            DiscountNote = NormalizeNote(discountNote);
            EnrolledAtUtc = enrolledAtUtc;
            WithdrawnAtUtc = withdrawnAtUtc;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private ClassGroupEnrollment(int id, int classGroupId, int studentId, int annualEnrollmentId,
            EnrollmentStatus status, long snapshotUnitPriceCentimes, long agreedUnitPriceCentimes, string? discountNote,
            DateTime enrolledAtUtc, DateTime? withdrawnAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(classGroupId, studentId, annualEnrollmentId, status, snapshotUnitPriceCentimes, agreedUnitPriceCentimes,
                   discountNote, enrolledAtUtc, withdrawnAtUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static ClassGroupEnrollment Create(int classGroupId, int studentId, int annualEnrollmentId,
            long snapshotUnitPriceCentimes, long agreedUnitPriceCentimes, string? discountNote,
            DateTime utcNow, int? createdByUserId)
        {
            return new ClassGroupEnrollment(classGroupId, studentId, annualEnrollmentId,
                EnrollmentStatus.Active, snapshotUnitPriceCentimes, agreedUnitPriceCentimes, discountNote,
                utcNow, null, utcNow, createdByUserId, null, null);
        }

        public static ClassGroupEnrollment Load(int id, int classGroupId, int studentId, int annualEnrollmentId,
            EnrollmentStatus status, long snapshotUnitPriceCentimes, long agreedUnitPriceCentimes, string? discountNote,
            DateTime enrolledAtUtc, DateTime? withdrawnAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new ClassGroupEnrollment(id, classGroupId, studentId, annualEnrollmentId, status,
                snapshotUnitPriceCentimes, agreedUnitPriceCentimes, discountNote, enrolledAtUtc, withdrawnAtUtc,
                createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public bool IsActive => Status == EnrollmentStatus.Active;

        /// <summary>الانسحاب من الفوج (D-53) — خاملاً إن كان منسحباً أصلاً</summary>
        public void Withdraw(DateTime utcNow, int? updatedByUserId)
        {
            if (Status == EnrollmentStatus.Withdrawn)
                return;

            Status = EnrollmentStatus.Withdrawn;
            WithdrawnAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        private static string? NormalizeNote(string? note)
            => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        private static void Validate(int classGroupId, int studentId, int annualEnrollmentId,
            long snapshotUnitPriceCentimes, long agreedUnitPriceCentimes, string? discountNote)
        {
            if (classGroupId <= 0)
                throw new DomainException("التسجيل يجب أن يتبع فوجاً.");
            if (studentId <= 0)
                throw new DomainException("التسجيل يجب أن يتبع طالباً.");
            if (annualEnrollmentId <= 0)
                throw new DomainException("تسجيل الفوج يستلزم تسجيلاً سنوياً.");
            if (snapshotUnitPriceCentimes < 0 || agreedUnitPriceCentimes < 0)
                throw new DomainException("السعر لا يمكن أن يكون سالباً.");
            if (discountNote is not null && discountNote.Trim().Length > 200)
                throw new DomainException("ملاحظة الخصم طويلة جداً (الحد الأقصى 200 حرف).");
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