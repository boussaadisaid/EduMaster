using EduMaster.Domain.Common;

namespace EduMaster.Domain.Users
{
    public sealed class UserAccount
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);   // ح-5: قفل 5 دقائق

        public int Id { get; private set; }
        public int PersonId { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public int FailedLoginCount { get; private set; }
        public DateTime? LastLoginAtUtc { get; private set; }
        public bool MustChangePassword { get; private set; }
        public DateTime? LockedUntilUtc { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private UserAccount(int personId, string username, string passwordHash, bool isActive, int failedLoginCount,
            DateTime? lastLoginAtUtc, bool mustChangePassword, DateTime? lockedUntilUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            PersonId = personId;
            Username = username;
            PasswordHash = passwordHash;
            IsActive = isActive;
            FailedLoginCount = failedLoginCount;
            LastLoginAtUtc = lastLoginAtUtc;
            MustChangePassword = mustChangePassword;
            LockedUntilUtc = lockedUntilUtc;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load — يفوّض ثم يضيف الحارس
        private UserAccount(int id, int personId, string username, string passwordHash, bool isActive, int failedLoginCount,
            DateTime? lastLoginAtUtc, bool mustChangePassword, DateTime? lockedUntilUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
            : this(personId, username, passwordHash, isActive, failedLoginCount, lastLoginAtUtc,
                   mustChangePassword, lockedUntilUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static UserAccount Create(int personId, string username, string passwordHash,
            DateTime createdAtUtc, int? createdByUserId, bool mustChangePassword = true)
        {
            if (personId <= 0)
                throw new DomainException("الحساب يجب أن يرتبط بشخص.");
            if (string.IsNullOrWhiteSpace(username))
                throw new DomainException("اسم المستخدم لا يمكن أن يكون فارغاً.");
            if (username.Trim().Length < 3)
                throw new DomainException("اسم المستخدم يجب أن يكون من 3 أحرف على الأقل.");
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new DomainException("كلمة المرور لا يمكن أن تكون فارغة.");

            return new UserAccount(personId, username.Trim(), passwordHash, isActive: true, failedLoginCount: 0,
                lastLoginAtUtc: null, mustChangePassword, lockedUntilUtc: null,
                createdAtUtc, createdByUserId, updatedAtUtc: null, updatedByUserId: null);
        }

        public static UserAccount Load(int id, int personId, string username, string passwordHash, bool isActive,
            int failedLoginCount, DateTime? lastLoginAtUtc, bool mustChangePassword, DateTime? lockedUntilUtc,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new UserAccount(id, personId, username, passwordHash, isActive, failedLoginCount, lastLoginAtUtc,
                mustChangePassword, lockedUntilUtc, createdAtUtc, createdByUserId, updatedAtUtc, updatedByUserId);
        }

        // ---- القفل الزمني: الكيان لا يقرأ الساعة — الوقت يُمرَّر صراحة (D-20) ----

        public bool IsLockedOut(DateTime utcNow) =>
            LockedUntilUtc is not null && LockedUntilUtc > utcNow;

        public TimeSpan? RemainingLockout(DateTime utcNow) =>
            IsLockedOut(utcNow) ? LockedUntilUtc!.Value - utcNow : null;

        public void RegisterFailedLogin(DateTime utcNow)
        {
            // انتهاء القفل الزمني = صفحة جديدة بعدّاد نظيف
            if (LockedUntilUtc is not null && LockedUntilUtc <= utcNow)
            {
                LockedUntilUtc = null;
                FailedLoginCount = 0;
            }

            FailedLoginCount++;
            UpdatedAtUtc = utcNow;

            if (FailedLoginCount >= MaxFailedAttempts)
            {
                LockedUntilUtc = utcNow.Add(LockoutDuration);
                FailedLoginCount = 0;   // بعد انتهاء القفل: 5 محاولات جديدة كاملة
            }
        }

        public void RegisterSuccessfulLogin(DateTime utcNow)
        {
            FailedLoginCount = 0;
            LockedUntilUtc = null;
            LastLoginAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>فك قفل يدوي بواسطة المدير — المخرج الثاني بعد الشفاء الذاتي الزمني</summary>
        public void Unlock(DateTime utcNow, int? unlockedByUserId)
        {
            LockedUntilUtc = null;
            FailedLoginCount = 0;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = unlockedByUserId;
        }

        /// <summary>المستخدم يغيّر كلمته بنفسه (شاشة الإلزام عند الدخول) — يزيل علم الإلزام</summary>
        public void ChangePasswordHash(string newHash, DateTime utcNow, int? updatedByUserId)
        {
            if (string.IsNullOrWhiteSpace(newHash))
                throw new DomainException("كلمة المرور الجديدة لا يمكن أن تكون فارغة.");

            PasswordHash = newHash;
            MustChangePassword = false;
            FailedLoginCount = 0;
            LockedUntilUtc = null;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        /// <summary>المدير يعيد تعيين كلمة شخص آخر — كلمة مؤقتة + إلزام التغيير + فك القفل ضمنياً</summary>
        public void AdminResetPasswordHash(string newHash, DateTime utcNow, int? adminUserId)
        {
            if (string.IsNullOrWhiteSpace(newHash))
                throw new DomainException("كلمة المرور الجديدة لا يمكن أن تكون فارغة.");

            PasswordHash = newHash;
            MustChangePassword = true;
            FailedLoginCount = 0;
            LockedUntilUtc = null;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = adminUserId;
        }

        public void Deactivate(DateTime utcNow, int? updatedByUserId)
        {
            if (!IsActive) return;
            IsActive = false;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
        }

        public void Activate(DateTime utcNow, int? updatedByUserId)
        {
            if (IsActive) return;
            IsActive = true;
            UpdatedAtUtc = utcNow;
            UpdatedByUserId = updatedByUserId;
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