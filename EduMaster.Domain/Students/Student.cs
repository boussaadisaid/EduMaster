using EduMaster.Domain.Common;

namespace EduMaster.Domain.Students
{
    public sealed class Student
    {
        public int Id { get; private set; }
        public int PersonId { get; private set; }
        public int? GuardianPersonId { get; private set; }
        public StudentCategory Category { get; private set; }
        public string? Notes { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Student(int personId, int? guardianPersonId, StudentCategory category, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(personId, guardianPersonId, category, notes);

            PersonId = personId;
            GuardianPersonId = guardianPersonId;
            Category = category;
            Notes = notes?.Trim();
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private Student(int id, int personId, int? guardianPersonId, StudentCategory category, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(personId, guardianPersonId, category, notes, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Student Create(int personId, int? guardianPersonId, StudentCategory category, string? notes,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new Student(personId, guardianPersonId, category, notes, createdAtUtc, createdByUserId, null, null);
        }

        public static Student Load(int id, int personId, int? guardianPersonId, StudentCategory category, string? notes,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Student(id, personId, guardianPersonId, category, notes, createdAtUtc, createdByUserId,
                updatedAtUtc, updatedByUserId);
        }

        public void Update(int? guardianPersonId, StudentCategory category, string? notes, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(PersonId, guardianPersonId, category, notes);

            GuardianPersonId = guardianPersonId;
            Category = category;
            Notes = notes?.Trim();
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(int personId, int? guardianPersonId, StudentCategory category, string? notes)
        {
            if (personId <= 0)
                throw new DomainException("ملف الطالب يجب أن يرتبط بشخص.");
            if (guardianPersonId is not null && guardianPersonId <= 0)
                throw new DomainException("ولي الأمر غير صالح.");
            if (!Enum.IsDefined(category))
                throw new DomainException("صنف الطالب غير صالح.");
            if (guardianPersonId == personId)
                throw new DomainException("لا يمكن أن يكون الطالب وليَّ نفسه.");
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