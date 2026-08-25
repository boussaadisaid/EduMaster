using EduMaster.Domain.Common;

namespace EduMaster.Domain.Payroll
{
    /// <summary>
    /// سياسة أجر موحّدة (D-113/D-114): ثلاثة أنواع للأساتذة (لكل حاضر/نسبة/بالساعة) ونوعان للموظفين (باليوم/شهري).
    /// افتراضية على الأستاذ (ClassGroupId فارغ) + تجاوز اختياري لفوج · الموظف سياسة واحدة بلا فوج.
    /// القيمة في RateCentimes للأنواع الثابتة، وفي Percentage للنسبة (واحدة فقط تحمل — القاعدة قاعدةً وكياناً).
    /// علم الغياب غير المبرر لكل أستاذ — الافتراضي لا يُحتسب (D-114) ولا معنى له للموظفين.
    /// الهوية (المستفيد/الفوج) ثابتة بعد الإنشاء (روح D-61) — التعديل على النوع والقيمة والعلم فقط.
    /// </summary>
    public sealed class PayPolicy
    {
        public int Id { get; private set; }
        public PayeeKind PayeeKind { get; private set; }
        public int? TeacherId { get; private set; }
        public int? EmployeeId { get; private set; }
        public int? ClassGroupId { get; private set; }
        public PayPolicyKind Kind { get; private set; }
        public long RateCentimes { get; private set; }
        public decimal? Percentage { get; private set; }
        public bool CountsUnjustifiedAbsent { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private PayPolicy(PayeeKind payeeKind, int? teacherId, int? employeeId, int? classGroupId,
            PayPolicyKind kind, long rateCentimes, decimal? percentage, bool countsUnjustifiedAbsent, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(payeeKind, teacherId, employeeId, classGroupId, kind, rateCentimes, percentage);

            PayeeKind = payeeKind;
            TeacherId = teacherId;
            EmployeeId = employeeId;
            ClassGroupId = classGroupId;
            Kind = kind;
            RateCentimes = rateCentimes;
            Percentage = percentage;
            CountsUnjustifiedAbsent = countsUnjustifiedAbsent;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private PayPolicy(int id, PayeeKind payeeKind, int? teacherId, int? employeeId, int? classGroupId,
            PayPolicyKind kind, long rateCentimes, decimal? percentage, bool countsUnjustifiedAbsent, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(payeeKind, teacherId, employeeId, classGroupId, kind, rateCentimes, percentage,
                countsUnjustifiedAbsent, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static PayPolicy Create(PayeeKind payeeKind, int? teacherId, int? employeeId, int? classGroupId,
            PayPolicyKind kind, long rateCentimes, decimal? percentage, bool countsUnjustifiedAbsent,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new PayPolicy(payeeKind, teacherId, employeeId, classGroupId, kind, rateCentimes, percentage,
                countsUnjustifiedAbsent, isActive: true, createdAtUtc, createdByUserId, null, null);
        }

        public static PayPolicy Load(int id, PayeeKind payeeKind, int? teacherId, int? employeeId, int? classGroupId,
            PayPolicyKind kind, long rateCentimes, decimal? percentage, bool countsUnjustifiedAbsent, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new PayPolicy(id, payeeKind, teacherId, employeeId, classGroupId, kind, rateCentimes, percentage,
                countsUnjustifiedAbsent, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        /// <summary>التعديل على النوع والقيمة والعلم فقط — الهوية (المستفيد/الفوج) ثابتة (روح D-61)</summary>
        public void Update(PayPolicyKind kind, long rateCentimes, decimal? percentage, bool countsUnjustifiedAbsent,
            DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(PayeeKind, TeacherId, EmployeeId, ClassGroupId, kind, rateCentimes, percentage);

            Kind = kind;
            RateCentimes = rateCentimes;
            Percentage = percentage;
            CountsUnjustifiedAbsent = countsUnjustifiedAbsent;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public void Deactivate(DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (!IsActive) return;
            IsActive = false;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public void Activate(DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (IsActive) return;
            IsActive = true;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(PayeeKind payeeKind, int? teacherId, int? employeeId, int? classGroupId,
            PayPolicyKind kind, long rateCentimes, decimal? percentage)
        {
            if (!Enum.IsDefined(payeeKind))
                throw new DomainException("نوع المستفيد غير صالح.");
            if (!Enum.IsDefined(kind))
                throw new DomainException("نوع سياسة الأجر غير صالح.");

            if (payeeKind == PayeeKind.Teacher)
            {
                if (teacherId is null or <= 0)
                    throw new DomainException("السياسة يجب أن تتبع أستاذاً.");
                if (employeeId is not null)
                    throw new DomainException("سياسة الأستاذ لا تتبع موظفاً.");
                if (kind is not (PayPolicyKind.PerPresentStudent or PayPolicyKind.Percentage or PayPolicyKind.PerHour))
                    throw new DomainException("نوع الأجر هذا للموظفين — أنواع الأستاذ: لكل حاضر، نسبة، بالساعة.");
            }
            else
            {
                if (employeeId is null or <= 0)
                    throw new DomainException("السياسة يجب أن تتبع موظفاً.");
                if (teacherId is not null)
                    throw new DomainException("سياسة الموظف لا تتبع أستاذاً.");
                if (classGroupId is not null)
                    throw new DomainException("التجاوز بالفوج للأساتذة فقط — الموظف له سياسة واحدة.");
                if (kind is not (PayPolicyKind.PerDay or PayPolicyKind.PerMonth))
                    throw new DomainException("نوع الأجر هذا للأساتذة — أنواع الموظف: باليوم، شهري ثابت.");
            }

            if (classGroupId is <= 0)
                throw new DomainException("معرّف الفوج غير صالح.");

            if (kind == PayPolicyKind.Percentage)
            {
                if (percentage is null or <= 0 or > 100)
                    throw new DomainException("النسبة يجب أن تكون أكبر من 0 ولا تتجاوز 100.");
                if (rateCentimes != 0)
                    throw new DomainException("سياسة النسبة لا تحمل قيمة ثابتة — القيمة في حقل النسبة.");
            }
            else
            {
                if (percentage is not null)
                    throw new DomainException("هذا النوع لا يحمل نسبة — القيمة في حقل الأجر الثابت.");
                if (rateCentimes <= 0)
                    throw new DomainException("قيمة الأجر يجب أن تكون أكبر من صفر.");
            }
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