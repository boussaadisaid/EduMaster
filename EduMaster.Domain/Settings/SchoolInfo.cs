using EduMaster.Domain.Common;

namespace EduMaster.Domain.Settings
{
    /// <summary>
    /// هوية المدرسة للمطبوعات (ط-7/D-130) — جدول صف واحد (Id=1 قسرياً في القاعدة).
    /// السقوط الافتراضي «EduMaster» يعيش في طبقة القراءة (DTO) لا في الكيان (D-131) ·
    /// اللوغو اسم ملف فقط عبر قناة IImageStore (D-38) · هاتف المدرسة نص حر بطول — لا كائن Phone (حروفه العشرية للأشخاص فقط).
    /// </summary>
    public sealed class SchoolInfo
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string? Phone { get; private set; }
        public string? Address { get; private set; }
        public string? LogoPath { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private SchoolInfo(string name, string? phone, string? address, string? logoPath,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(name, phone, address, logoPath);

            Name = name.Trim();
            Phone = NormalizeOptional(phone);
            Address = NormalizeOptional(address);
            LogoPath = NormalizeOptional(logoPath);
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف حارس الهوية
        private SchoolInfo(int id, string name, string? phone, string? address, string? logoPath,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(name, phone, address, logoPath, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static SchoolInfo Create(string name, string? phone, string? address,
            DateTime utcNow, int? createdByUserId)
        {
            return new SchoolInfo(name, phone, address, null, utcNow, createdByUserId, null, null);
        }

        public static SchoolInfo Load(int id, string name, string? phone, string? address, string? logoPath,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new SchoolInfo(id, name, phone, address, logoPath,
                createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        /// <summary>تحرير الهوية — اللوغو وحده خارجها (ChangeLogo منفصلة كقناة الصور D-38)</summary>
        public void Update(string name, string? phone, string? address, DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(name, phone, address, LogoPath);

            Name = name.Trim();
            Phone = NormalizeOptional(phone);
            Address = NormalizeOptional(address);
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>اللوغو — null = إزالة (مرآة ChangePhoto للأشخاص)</summary>
        public void ChangeLogo(string? logoPath, DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (logoPath is not null && logoPath.Trim().Length > 260)
                throw new DomainException("مسار اللوغو طويل جداً.");

            LogoPath = NormalizeOptional(logoPath);
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static void Validate(string name, string? phone, string? address, string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("اسم المدرسة مطلوب.");
            if (name.Trim().Length > 100)
                throw new DomainException("اسم المدرسة طويل جداً (الحد الأقصى 100 حرف).");
            if (phone is not null && phone.Trim().Length > 50)
                throw new DomainException("هاتف المدرسة طويل جداً (الحد الأقصى 50 حرفاً).");
            if (address is not null && address.Trim().Length > 200)
                throw new DomainException("عنوان المدرسة طويل جداً (الحد الأقصى 200 حرف).");
            if (logoPath is not null && logoPath.Trim().Length > 260)
                throw new DomainException("مسار اللوغو طويل جداً.");
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