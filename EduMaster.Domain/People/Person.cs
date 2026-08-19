using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People.ValueObjects;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;


namespace EduMaster.Domain.People
{
    public sealed class Person
    {
        // Properties
        public int Id { get; private set; }
        public FirstName FirstName { get; private set; }
        public LastName LastName { get; private set; }
        public FirstName? FatherName { get; private set; }        
        public BirthDate? BirthDate { get; private set; }
        public GenderType? Gender { get; private set; }
        public Phone? Phone { get; private set; } = null;
        public Email? Email { get; private set; } = null;
        public string? Address { get; private set; }
        public string? PhotoPath { get; private set; }
        public bool IsActive { get; private set; } = true;
        public string? FullNameNormalized { get; private set; }      
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet = false;

        //Constractor for add new
        private Person(FirstName firstName,LastName lastName,FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone,Email? email, string? address, string? photoPath, bool isActive,
            DateTime createdAtUtc,int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            FirstName = firstName;
            LastName = lastName;

            FatherName = fatherName;
            BirthDate = birthDate;
            Gender = gender;

            Phone = phone;
            Email = email;

            Address = address?.Trim();
            PhotoPath = photoPath;

            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;


            // *** أهم سطر ***
            FullNameNormalized =
                NormalizeFullName(firstName.Value, lastName.Value);
        }


        // Constractor for Loading from database
        private Person(int id, FirstName firstName, LastName lastName, FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone, Email? email, string? address, string? photoPath, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
            FatherName = fatherName;
            BirthDate = birthDate;
            Gender = gender;
            Phone = phone;
            Email = email;
            Address = address;
            PhotoPath = photoPath;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;

            FullNameNormalized =
                NormalizeFullName(firstName.Value, lastName.Value);

            _idSet = true;
        }




        //Factory method
        public static Person Create(FirstName firstName,LastName lastName, FirstName? fatherName,BirthDate? birthDate,
            GenderType? gender,Phone? phone, Email? email, string? address, string? photoPath, int? createdByUserId,
            DateTime createdAtUtc)
        {
            return new Person(firstName, lastName, fatherName, birthDate, gender, phone, email, address,
                photoPath, isActive: true, createdAtUtc: createdAtUtc, createdByUserId, updatedAtUtc: null,
                updatedByUserId: null);
        }



        // أضف دالة لتحديث الشخص
        public void Update(FirstName firstName,LastName lastName, FirstName? fatherName, BirthDate? birthDate,
            GenderType? gender, Phone? phone,Email? email, string? address,string? photoPath, DateTime updatedAtUtc,
            int updatedByUserId)
        {
            FirstName = firstName;
            LastName = lastName;

            // تحديث FullNameNormalized عند تغيير الاسم
            FullNameNormalized = NormalizeFullName(FirstName.Value, LastName.Value);

            FatherName = fatherName;
            BirthDate = birthDate;
            Gender = gender;
            Phone = phone;
            Email = email;
            Address = address?.Trim();
            PhotoPath = photoPath;
          
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public static Person Load(
            int id,
            FirstName firstName,
            LastName lastName,
            FirstName? fatherName,
            BirthDate? birthDate,
            GenderType? gender,
            Phone? phone,
            Email? email,
            string? address,
            string? photoPath,
            bool isActive,
            DateTime createdAtUtc,
            int? createdByUserId,
            DateTime? updatedAtUtc,
            int? updatedByUserId)
        {
            return new Person(
                id,
                firstName,
                lastName,
                fatherName,
                birthDate,
                gender,
                phone,
                email,
                address,
                photoPath,
                isActive,
                createdAtUtc,
                createdByUserId,
                updatedAtUtc,
                updatedByUserId);
        }


        internal void SetId(int id)
        {
            if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
            if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }


        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;

        private static string NormalizeFullName(string first, string last)
        {
            string raw = $"{last} {first}".Trim();

            raw = raw.ToLowerInvariant();
            raw = RemoveDiacritics(raw);

            // Replace multiple spaces
            raw = Regex.Replace(raw, @"\s+", " ");

            return raw;
        }
        private static string RemoveDiacritics(string text)
        {
            var formD = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var ch in formD)
            {
                var unicode = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicode != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

       



    } 
}