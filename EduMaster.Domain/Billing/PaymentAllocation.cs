using EduMaster.Domain.Common;

namespace EduMaster.Domain.Billing
{
    /// <summary>سطر تخصيص: جزء من دفعة يسدّد مستحقاً (D-106) — يُكتب مع دفعته في معاملة واحدة · بلا Load (لا قراءة كيانية)</summary>
    public sealed class PaymentAllocation
    {
        public int Id { get; private set; }
        public int PaymentId { get; private set; }
        public int ChargeId { get; private set; }
        public long AmountCentimes { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        private PaymentAllocation(int paymentId, int chargeId, long amountCentimes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            if (paymentId <= 0)
                throw new DomainException("التخصيص يجب أن يتبع دفعة.");
            if (chargeId <= 0)
                throw new DomainException("التخصيص يجب أن يتبع مستحقاً.");
            if (amountCentimes <= 0)
                throw new DomainException("مبلغ التخصيص يجب أن يكون أكبر من صفر.");

            PaymentId = paymentId;
            ChargeId = chargeId;
            AmountCentimes = amountCentimes;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public static PaymentAllocation Create(int paymentId, int chargeId, long amountCentimes,
            DateTime utcNow, int? createdByUserId)
        {
            return new PaymentAllocation(paymentId, chargeId, amountCentimes, utcNow, createdByUserId, null, null);
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