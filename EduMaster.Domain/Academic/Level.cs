using EduMaster.Domain.Common;

namespace EduMaster.Domain.Academic
{
    public sealed class Level
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Level(string name, int sortOrder, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            ValidateName(name);

            Name = name.Trim();
            SortOrder = sortOrder;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Level(int id, string name, int sortOrder, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(name, sortOrder, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Level Create(string name, int sortOrder, DateTime createdAtUtc, int? createdByUserId)
        {
            return new Level(name, sortOrder, true, createdAtUtc, createdByUserId, null, null);
        }

        public static Level Load(int id, string name, int sortOrder, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Level(id, name, sortOrder, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public void Update(string name, int sortOrder, DateTime updatedAtUtc, int? updatedByUserId)
        {
            ValidateName(name);

            Name = name.Trim();
            SortOrder = sortOrder;
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
                throw new DomainException("أدخل اسم المستوى.");
            if (name.Trim().Length > 100)
                throw new DomainException("اسم المستوى طويل جداً (الحد الأقصى 100 حرف).");
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