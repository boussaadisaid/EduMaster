using EduMaster.Domain.Common;

namespace EduMaster.Domain.Academic
{
    public sealed class Room
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int? Capacity { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Room(string name, int? capacity, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(name, capacity);

            Name = name.Trim();
            Capacity = capacity;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Room(int id, string name, int? capacity, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(name, capacity, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Room Create(string name, int? capacity, DateTime createdAtUtc, int? createdByUserId)
        {
            return new Room(name, capacity, true, createdAtUtc, createdByUserId, null, null);
        }

        public static Room Load(int id, string name, int? capacity, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Room(id, name, capacity, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public void Update(string name, int? capacity, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(name, capacity);

            Name = name.Trim();
            Capacity = capacity;
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

        private static void Validate(string? name, int? capacity)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("أدخل اسم القاعة.");
            if (name.Trim().Length > 50)
                throw new DomainException("اسم القاعة طويل جداً (الحد الأقصى 50 حرفاً).");
            if (capacity is <= 0)
                throw new DomainException("سعة القاعة يجب أن تكون أكبر من صفر (أو تُترك فارغة).");
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