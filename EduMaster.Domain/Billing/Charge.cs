using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Billing
{
    /// <summary>
    /// مستحق مالي على طالب (D-103): يتولد ذرّياً من مصدره (تسجيل سنوي ← حقوق · مشتراة ← حزمة) —
    /// مصنعان مسمّيان يفرضان إقران المصدر بالنوع (يقابلهما CK_Charges_Source في القاعدة).
    /// لا حذف ولا تعديل صامت (D-108): Cancel/Reduce موثقتان بسبب إلزامي، والمبلغ الأصلي محفوظ للتدقيق.
    /// </summary>
    public sealed class Charge
    {
        public int Id { get; private set; }
        public int StudentId { get; private set; }
        public ChargeKind Kind { get; private set; }
        public int? AnnualEnrollmentId { get; private set; }
        public int? GroupSessionPurchaseId { get; private set; }
        public long OriginalAmountCentimes { get; private set; }
        public long AmountCentimes { get; private set; }
        public ChargeStatus Status { get; private set; }
        public string? AdjustmentNote { get; private set; }
        public DateTime? CancelledAtUtc { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Charge(int studentId, ChargeKind kind, int? annualEnrollmentId, int? groupSessionPurchaseId,
            long originalAmountCentimes, long amountCentimes, ChargeStatus status, string? adjustmentNote, DateTime? cancelledAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(studentId, kind, annualEnrollmentId, groupSessionPurchaseId, originalAmountCentimes, amountCentimes, adjustmentNote);

            StudentId = studentId;
            Kind = kind;
            AnnualEnrollmentId = annualEnrollmentId;
            GroupSessionPurchaseId = groupSessionPurchaseId;
            OriginalAmountCentimes = originalAmountCentimes;
            AmountCentimes = amountCentimes;
            Status = status;
            AdjustmentNote = string.IsNullOrWhiteSpace(adjustmentNote) ? null : adjustmentNote.Trim();
            CancelledAtUtc = cancelledAtUtc;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Charge(int id, int studentId, ChargeKind kind, int? annualEnrollmentId, int? groupSessionPurchaseId,
            long originalAmountCentimes, long amountCentimes, ChargeStatus status, string? adjustmentNote, DateTime? cancelledAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(studentId, kind, annualEnrollmentId, groupSessionPurchaseId, originalAmountCentimes, amountCentimes,
                   status, adjustmentNote, cancelledAtUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        /// <summary>D-103: مستحق حقوق التسجيل — يُستدعى داخل معاملة التسجيل (لا يُستدعى عند 0 = إعفاء)</summary>
        public static Charge CreateForRegistrationFee(int studentId, int annualEnrollmentId, long amountCentimes,
            DateTime utcNow, int? createdByUserId)
        {
            return new Charge(studentId, ChargeKind.RegistrationFee, annualEnrollmentId, null,
                amountCentimes, amountCentimes, ChargeStatus.Active, null, null,
                utcNow, createdByUserId, null, null);
        }

        /// <summary>D-103/D-96: مستحق حزمة حصص = عدد × سعر الحصة المتفق — داخل معاملة الشراء (لا يُستدعى عند 0)</summary>
        public static Charge CreateForSessionBundle(int studentId, int groupSessionPurchaseId, long amountCentimes,
            DateTime utcNow, int? createdByUserId)
        {
            return new Charge(studentId, ChargeKind.SessionBundle, null, groupSessionPurchaseId,
                amountCentimes, amountCentimes, ChargeStatus.Active, null, null,
                utcNow, createdByUserId, null, null);
        }

        public static Charge Load(int id, int studentId, ChargeKind kind, int? annualEnrollmentId, int? groupSessionPurchaseId,
            long originalAmountCentimes, long amountCentimes, ChargeStatus status, string? adjustmentNote, DateTime? cancelledAtUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Charge(id, studentId, kind, annualEnrollmentId, groupSessionPurchaseId, originalAmountCentimes, amountCentimes,
                status, adjustmentNote, cancelledAtUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        /// <summary>D-108: إلغاء موثق بسبب — المبالغ تبقى للتدقيق، والمسوّى لا يمسه التخصيص (4.2)</summary>
        public void Cancel(string reason, DateTime utcNow, int? updatedByUserId)
        {
            if (Status != ChargeStatus.Active)
                throw new DomainException("هذا المستحق مسوّى بالفعل.");
            SetAdjustmentReason(reason);

            Status = ChargeStatus.Cancelled;
            CancelledAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>D-108: تخفيض موثق (مبلغ جديد أقل + سبب) — صفر مسموح (إعفاء ما بعد الاتفاق)، والزيادة ليست من هنا</summary>
        public void Reduce(long newAmountCentimes, string reason, DateTime utcNow, int? updatedByUserId)
        {
            if (Status != ChargeStatus.Active)
                throw new DomainException("لا يمكن تخفيض مستحق مسوّى.");
            if (newAmountCentimes < 0)
                throw new DomainException("المبلغ الجديد لا يمكن أن يكون سالباً.");
            if (newAmountCentimes >= AmountCentimes)
                throw new DomainException("التخفيض يقتضي مبلغاً جديداً أقل من الحالي.");
            SetAdjustmentReason(reason);

            AmountCentimes = newAmountCentimes;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        private void SetAdjustmentReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException("سبب التسوية إلزامي (إلغاء أو تخفيض — D-108).");
            if (reason.Trim().Length > 200)
                throw new DomainException("سبب التسوية طويل جداً (الحد الأقصى 200 حرف).");

            AdjustmentNote = reason.Trim();
        }

        private static void Validate(int studentId, ChargeKind kind, int? annualEnrollmentId, int? groupSessionPurchaseId,
            long originalAmountCentimes, long amountCentimes, string? adjustmentNote)
        {
            if (studentId <= 0)
                throw new DomainException("المستحق يجب أن يتبع طالباً.");
            if (!Enum.IsDefined(kind))
                throw new DomainException("نوع المستحق غير صالح.");

            // إقران المصدر بالنوع — يقابله CK_Charges_Source في القاعدة
            if (kind == ChargeKind.RegistrationFee && (annualEnrollmentId is null or <= 0 || groupSessionPurchaseId is not null))
                throw new DomainException("مستحق الحقوق يتبع تسجيلاً سنوياً فقط.");
            if (kind == ChargeKind.SessionBundle && (groupSessionPurchaseId is null or <= 0 || annualEnrollmentId is not null))
                throw new DomainException("مستحق الحزمة يتبع مشتراة فقط.");

            if (originalAmountCentimes <= 0)
                throw new DomainException("المبلغ الأصلي يجب أن يكون أكبر من صفر (الصفر لا يولّد مستحقاً — D-103).");
            if (amountCentimes < 0 || amountCentimes > originalAmountCentimes)
                throw new DomainException("المبلغ الحالي خارج الحدود (0 … الأصلي).");
            if (adjustmentNote is not null && adjustmentNote.Trim().Length > 200)
                throw new DomainException("سبب التسوية طويل جداً (الحد الأقصى 200 حرف).");
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