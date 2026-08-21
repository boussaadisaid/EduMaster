using EduMaster.Domain.Common;

namespace EduMaster.Domain.Academic
{
    public sealed class Subject
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Subject(string name, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            ValidateName(name);

            Name = name.Trim();
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Subject(int id, string name, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(name, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Subject Create(string name, DateTime createdAtUtc, int? createdByUserId)
        {
            return new Subject(name, true, createdAtUtc, createdByUserId, null, null);
        }

        public static Subject Load(int id, string name, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Subject(id, name, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public void Update(string name, DateTime updatedAtUtc, int? updatedByUserId)
        {
            ValidateName(name);

            Name = name.Trim();
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

        private static void ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("أدخل اسم المادة.");
            if (name.Trim().Length > 100)
                throw new DomainException("اسم المادة طويل جداً (الحد الأقصى 100 حرف).");
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