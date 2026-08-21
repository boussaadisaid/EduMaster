using EduMaster.Domain.Common;

namespace EduMaster.Domain.Teachers
{
    public sealed class Teacher
    {
        public int Id { get; private set; }
        public int PersonId { get; private set; }
        public string? Specialty { get; private set; }
        public string? Notes { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Teacher(int personId, string? specialty, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(personId, specialty, notes);

            PersonId = personId;
            Specialty = specialty?.Trim();
            Notes = notes?.Trim();
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Teacher(int id, int personId, string? specialty, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(personId, specialty, notes, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Teacher Create(int personId, string? specialty, string? notes,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new Teacher(personId, specialty, notes, createdAtUtc, createdByUserId, null, null);
        }

        public static Teacher Load(int id, int personId, string? specialty, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Teacher(id, personId, specialty, notes, createdAtUtc, createdByUserId,
                updatedAtUtc, updatedByUserId);
        }

        public void Update(string? specialty, string? notes, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(PersonId, specialty, notes);

            Specialty = specialty?.Trim();
            Notes = notes?.Trim();
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(int personId, string? specialty, string? notes)
        {
            if (personId <= 0)
                throw new DomainException("ملف الأستاذ يجب أن يرتبط بشخص.");
            if (specialty?.Trim().Length > 100)
                throw new DomainException("التخصص طويل جداً (الحد الأقصى 100 حرف).");
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