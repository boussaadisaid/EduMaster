using EduMaster.Domain.Common;

namespace EduMaster.Domain.Employees
{
    /// <summary>
    /// ملف موظف (D-115): 1:1 فوق نواة Person بمرآة Teacher حرفاً — الوظيفة نصاً حراً إلزامية (سكربت 015).
    /// الحذف منطقي عبر المستودع (IsDeleted + فهرس مفلتر — نمط D-39)، فالكيان لا يحمله — مثل Teacher تماماً.
    /// </summary>
    public sealed class Employee
    {
        public int Id { get; private set; }
        public int PersonId { get; private set; }
        public string JobTitle { get; private set; }
        public string? Notes { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Employee(int personId, string jobTitle, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(personId, jobTitle, notes);

            PersonId = personId;
            JobTitle = jobTitle.Trim();
            Notes = notes?.Trim();
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Employee(int id, int personId, string jobTitle, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(personId, jobTitle, notes, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Employee Create(int personId, string jobTitle, string? notes,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new Employee(personId, jobTitle, notes, createdAtUtc, createdByUserId, null, null);
        }

        public static Employee Load(int id, int personId, string jobTitle, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Employee(id, personId, jobTitle, notes, createdAtUtc, createdByUserId,
                updatedAtUtc, updatedByUserId);
        }

        public void Update(string jobTitle, string? notes, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(PersonId, jobTitle, notes);

            JobTitle = jobTitle.Trim();
            Notes = notes?.Trim();
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(int personId, string jobTitle, string? notes)
        {
            if (personId <= 0)
                throw new DomainException("ملف الموظف يجب أن يرتبط بشخص.");
            if (string.IsNullOrWhiteSpace(jobTitle))
                throw new DomainException("الوظيفة مطلوبة.");
            if (jobTitle.Trim().Length > 100)
                throw new DomainException("الوظيفة طويلة جداً (الحد الأقصى 100 حرف).");
            if (notes?.Trim().Length > 500)
                throw new DomainException("الملاحظات طويلة جداً (الحد الأقصى 500 حرف).");
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