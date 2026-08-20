using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People.ValueObjects;

namespace EduMaster.Domain.People
{
    public sealed class Person
    {
        public int Id { get; private set; }
        public FirstName FirstName { get; private set; }
        public LastName LastName { get; private set; }
        public FirstName? FatherName { get; private set; }
        public BirthDate? BirthDate { get; private set; }
        public GenderType? Gender { get; private set; }
        public Phone? Phone { get; private set; }
        public Phone? Phone2 { get; private set; }
        public Email? Email { get; private set; }
        public string? Address { get; private set; }
        public string? PhotoPath { get; private set; }
        public bool IsActive { get; private set; }
        public string FullNameNormalized { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private Person(FirstName firstName, LastName lastName, FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone, Phone? phone2, Email? email, string? address, string? photoPath, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            FirstName = firstName;
            LastName = lastName;
            FatherName = fatherName;
            BirthDate = birthDate;
            Gender = gender;
            Phone = phone;
            Phone2 = phone2;
            Email = email;
            Address = address?.Trim();
            PhotoPath = photoPath;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;

            // الاسم الثلاثي المطبَّع — بالدالة المشتركة الوحيدة
            FullNameNormalized = BuildNormalizedName(firstName, fatherName, lastName);
        }

        // Constructor for Load
        private Person(int id, FirstName firstName, LastName lastName, FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone, Phone? phone2, Email? email, string? address, string? photoPath, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(firstName, lastName, fatherName, birthDate, gender, phone, phone2, email, address, photoPath,
                   isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static Person Create(FirstName firstName, LastName lastName, FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone, Phone? phone2, Email? email, string? address, string? photoPath,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new Person(firstName, lastName, fatherName, birthDate, gender, phone, phone2, email, address,
                photoPath, isActive: true, createdAtUtc, createdByUserId, updatedAtUtc: null, updatedByUserId: null);
        }

        public static Person Load(int id, FirstName firstName, LastName lastName, FirstName? fatherName,
            BirthDate? birthDate, GenderType? gender, Phone? phone, Phone? phone2, Email? email, string? address,
            string? photoPath, bool isActive, DateTime createdAtUtc, int? createdByUserId,
            DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new Person(id, firstName, lastName, fatherName, birthDate, gender, phone, phone2, email, address,
                photoPath, isActive, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public void Update(FirstName firstName, LastName lastName, FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone, Phone? phone2, Email? email, string? address, string? photoPath,
            DateTime updatedAtUtc, int? updatedByUserId)
        {
            FirstName = firstName;
            LastName = lastName;
            FatherName = fatherName;
            BirthDate = birthDate;
            Gender = gender;
            Phone = phone;
            Phone2 = phone2;
            Email = email;
            Address = address?.Trim();
            PhotoPath = photoPath;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;

            // إعادة حساب الاسم المطبَّع عند كل تعديل — الشفاء الذاتي للقيم القديمة
            FullNameNormalized = BuildNormalizedName(firstName, fatherName, lastName);
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

        internal void SetId(int id)
        {
            if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
            if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        private static string BuildNormalizedName(FirstName firstName, FirstName? fatherName, LastName lastName) =>
            ArabicTextNormalizer.Normalize($"{firstName.Value} {fatherName?.Value} {lastName.Value}");

        public override string ToString() => $"{FirstName} {LastName}";
    }
}