using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Billing
{
    /// <summary>
    /// إيصال قبض/صرف (D-104/D-105): لطالب واحد + دافع فعلي اختياري (الولي D-36) + رقم إيصال متسلسل يُسنَد داخل المعاملة
    /// + تاريخ عمل قابل للاختيار. وثيقة مالية لا تُعدَّل ولا تُحذف (D-109) — العكس بإيصال صرف (D-108).
    /// ملاحظة تصميمية: بلا Load — لا قراءة كيانية له (تاريخ المدفوعات قراءة مسطّحة في 4.3).
    /// </summary>
    public sealed class Payment
    {
        public int Id { get; private set; }
        public int ReceiptNo { get; private set; }
        public int StudentId { get; private set; }
        public int? PaidByPersonId { get; private set; }
        public int TreasuryAccountId { get; private set; }
        public PaymentKind Kind { get; private set; }
        public long AmountCentimes { get; private set; }
        public DateOnly PaidOn { get; private set; }
        public string? Note { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        private Payment(int studentId, int? paidByPersonId, int treasuryAccountId, PaymentKind kind, long amountCentimes, DateOnly paidOn,
            string? note, int receiptNo, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            if (studentId <= 0)
                throw new DomainException("الإيصال يجب أن يتبع طالباً.");
            if (!Enum.IsDefined(kind))
                throw new DomainException("نوع الإيصال غير صالح.");
            if (treasuryAccountId <= 0)
                throw new DomainException("الإيصال يجب أن يرتبط بحساب مالي.");
            if (treasuryAccountId <= 0)
                throw new DomainException("الإيصال يجب أن يرتبط بحساب مالي.");
            if (amountCentimes <= 0)
                throw new DomainException("مبلغ الإيصال يجب أن يكون أكبر من صفر.");
            if (receiptNo <= 0)
                throw new DomainException("رقم الإيصال يجب أن يكون أكبر من صفر.");
            if (note is not null && note.Trim().Length > 200)
                throw new DomainException("الملاحظة طويلة جداً (الحد الأقصى 200 حرف).");

            StudentId = studentId;
            PaidByPersonId = paidByPersonId;
            TreasuryAccountId = treasuryAccountId;
            Kind = kind;
            AmountCentimes = amountCentimes;
            PaidOn = paidOn;
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            ReceiptNo = receiptNo;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>receiptNo يُحسب داخل معاملة التسجيل (GetNextReceiptNoAsync) — والفهرس الفريد يحرسه (D-105)</summary>
        public static Payment Create(int studentId, int? paidByPersonId, int treasuryAccountId, PaymentKind kind, long amountCentimes, DateOnly paidOn,
            string? note, int receiptNo, DateTime utcNow, int? createdByUserId)
        {
            return new Payment(studentId, paidByPersonId, treasuryAccountId, kind, amountCentimes, paidOn, note,
                receiptNo, utcNow, createdByUserId, null, null);
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