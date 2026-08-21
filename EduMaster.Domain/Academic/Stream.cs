using EduMaster.Domain.Common;

namespace EduMaster.Domain.Academic
{
    public sealed class Stream
    {
        public int Id { get; private set; }
        public int LevelId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Stream(int levelId, string name, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(levelId, name);

            LevelId = levelId;
            Name = name.Trim();
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Stream(int id, int levelId, string name, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(levelId, name, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Stream Create(int levelId, string name, DateTime createdAtUtc, int? createdByUserId)
        {
            return new Stream(levelId, name, true, createdAtUtc, createdByUserId, null, null);
        }

        public static Stream Load(int id, int levelId, string name, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Stream(id, levelId, name, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public void Update(string name, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(LevelId, name);

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

        private static void Validate(int levelId, string? name)
        {
            if (levelId <= 0)
                throw new DomainException("الشعبة يجب أن تتبع مستوى.");
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("أدخل اسم الشعبة.");
            if (name.Trim().Length > 100)
                throw new DomainException("اسم الشعبة طويل جداً (الحد الأقصى 100 حرف).");
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