using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Enrollments
{
    /// <summary>
    /// التسجيل السنوي لطالب في سنة دراسية (D-37): يحمل المستوى/الشعبة السنويين وحقوق التسجيل المتفق عليها (D-52).
    /// الحالة نشط/منسحب فقط (D-53) — العودة بعد الانسحاب بصف جديد، والسنة ثابتة (الخطأ فيها = انسحاب + تسجيل جديد — D-72).
    /// </summary>
    public sealed class AnnualEnrollment
    {
        public int Id { get; private set; }
        public int StudentId { get; private set; }
        public int AcademicYearId { get; private set; }
        public int LevelId { get; private set; }
        public int? StreamId { get; private set; }
        public EnrollmentStatus Status { get; private set; }
        public long AgreedRegistrationFeeCentimes { get; private set; }
        public string? RegistrationFeeNote { get; private set; }
        public DateTime EnrolledAtUtc { get; private set; }
        public DateTime? WithdrawnAtUtc { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private AnnualEnrollment(int studentId, int academicYearId, int levelId, int? streamId,
            EnrollmentStatus status, long agreedRegistrationFeeCentimes, string? registrationFeeNote,
            DateTime enrolledAtUtc, DateTime? withdrawnAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(studentId, academicYearId, levelId, streamId, agreedRegistrationFeeCentimes, registrationFeeNote);

            StudentId = studentId;
            AcademicYearId = academicYearId;
            LevelId = levelId;
            StreamId = streamId;
            Status = status;
            AgreedRegistrationFeeCentimes = agreedRegistrationFeeCentimes;
            RegistrationFeeNote = NormalizeNote(registrationFeeNote);
            EnrolledAtUtc = enrolledAtUtc;
            WithdrawnAtUtc = withdrawnAtUtc;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private AnnualEnrollment(int id, int studentId, int academicYearId, int levelId, int? streamId,
            EnrollmentStatus status, long agreedRegistrationFeeCentimes, string? registrationFeeNote,
            DateTime enrolledAtUtc, DateTime? withdrawnAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(studentId, academicYearId, levelId, streamId, status, agreedRegistrationFeeCentimes, registrationFeeNote,
                   enrolledAtUtc, withdrawnAtUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static AnnualEnrollment Create(int studentId, int academicYearId, int levelId, int? streamId,
            long agreedRegistrationFeeCentimes, string? registrationFeeNote,
            DateTime utcNow, int? createdByUserId)
        {
            return new AnnualEnrollment(studentId, academicYearId, levelId, streamId,
                EnrollmentStatus.Active, agreedRegistrationFeeCentimes, registrationFeeNote,
                utcNow, null, utcNow, createdByUserId, null, null);
        }

        public static AnnualEnrollment Load(int id, int studentId, int academicYearId, int levelId, int? streamId,
            EnrollmentStatus status, long agreedRegistrationFeeCentimes, string? registrationFeeNote,
            DateTime enrolledAtUtc, DateTime? withdrawnAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new AnnualEnrollment(id, studentId, academicYearId, levelId, streamId, status,
                agreedRegistrationFeeCentimes, registrationFeeNote, enrolledAtUtc, withdrawnAtUtc,
                createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public bool IsActive => Status == EnrollmentStatus.Active;

        /// <summary>الحقوق تُعدَّل دائماً ما دام التسجيل نشطاً (D-72) — الإعفاء = صفر + ملاحظة</summary>
        public void UpdateRegistrationFee(long agreedRegistrationFeeCentimes, string? registrationFeeNote,
            DateTime updatedAtUtc, int? updatedByUserId)
        {
            EnsureActive();
            if (agreedRegistrationFeeCentimes < 0)
                throw new DomainException("حقوق التسجيل لا يمكن أن تكون سالبة.");
            if (registrationFeeNote is not null && registrationFeeNote.Trim().Length > 200)
                throw new DomainException("ملاحظة الحقوق طويلة جداً (الحد الأقصى 200 حرف).");

            AgreedRegistrationFeeCentimes = agreedRegistrationFeeCentimes;
            RegistrationFeeNote = NormalizeNote(registrationFeeNote);
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>تعديل المستوى/الشعبة — حارس «لا أفواج نشطة» في الـHandler (D-54/D-72)</summary>
        public void UpdateLevelStream(int levelId, int? streamId, DateTime updatedAtUtc, int? updatedByUserId)
        {
            EnsureActive();
            if (levelId <= 0)
                throw new DomainException("التسجيل يجب أن يتبع مستوى.");
            if (streamId is <= 0)
                throw new DomainException("معرّف الشعبة غير صالح.");

            LevelId = levelId;
            StreamId = streamId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>الانسحاب (D-53) — 2.4: الـHandler يسحب تسجيلات الأفواج النشطة في نفس المعاملة</summary>
        public void Withdraw(DateTime utcNow, int? updatedByUserId)
        {
            if (Status == EnrollmentStatus.Withdrawn)
                return;

            Status = EnrollmentStatus.Withdrawn;
            WithdrawnAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        private void EnsureActive()
        {
            if (Status != EnrollmentStatus.Active)
                throw new DomainException("لا يمكن تعديل تسجيل منسحب — سجّله من جديد بصف جديد.");
        }

        private static string? NormalizeNote(string? note)
            => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        private static void Validate(int studentId, int academicYearId, int levelId, int? streamId,
            long agreedRegistrationFeeCentimes, string? registrationFeeNote)
        {
            if (studentId <= 0)
                throw new DomainException("التسجيل يجب أن يتبع طالباً.");
            if (academicYearId <= 0)
                throw new DomainException("التسجيل يجب أن يتبع سنة دراسية.");
            if (levelId <= 0)
                throw new DomainException("التسجيل يجب أن يتبع مستوى.");
            if (streamId is <= 0)
                throw new DomainException("معرّف الشعبة غير صالح.");
            if (agreedRegistrationFeeCentimes < 0)
                throw new DomainException("حقوق التسجيل لا يمكن أن تكون سالبة.");
            if (registrationFeeNote is not null && registrationFeeNote.Trim().Length > 200)
                throw new DomainException("ملاحظة الحقوق طويلة جداً (الحد الأقصى 200 حرف).");
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