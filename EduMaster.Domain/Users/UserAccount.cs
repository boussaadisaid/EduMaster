using EduMaster.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Domain.Users
{
    public sealed class UserAccount
    {
        public int Id { get; private set; }
        public int PersonId { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public int FailedLoginCount { get; private set; }
        public DateTime? LastLoginAtUtc { get; private set; }
        public bool MustChangePassword { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet = false;

        public bool IsLockedOut => FailedLoginCount >= 5;

        // Constractor for creating a new user account
        private UserAccount(int personId, string username, string passwordHash, bool isActive, int failedLoginCount,
            DateTime? lastLoginAtUtc, bool mustChangePassword, DateTime createdAtUtc, int? createdByUserId,
            DateTime? updatedAtUtc, int? updatedByUserId)
        {
            PersonId = personId;
            Username = username;
            PasswordHash = passwordHash;
            IsActive = isActive;
            FailedLoginCount = failedLoginCount;
            LastLoginAtUtc = lastLoginAtUtc;
            MustChangePassword = mustChangePassword;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constractor for loading an existing user account from the database
        private UserAccount(int id, int personId, string username, string passwordHash, bool isActive, int failedLoginCount,
            DateTime? lastLoginAtUtc, bool mustChangePassword, DateTime createdAtUtc, int? createdByUserId,
            DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Id = id;
            PersonId = personId;
            Username = username;
            PasswordHash = passwordHash;
            IsActive = isActive;
            FailedLoginCount = failedLoginCount;
            LastLoginAtUtc = lastLoginAtUtc;
            MustChangePassword = mustChangePassword;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;

            _idSet = true;
        }


        // Factory method for creating a new user account
        public static UserAccount Create(int personId, string username, string passwordHash, int? createdByUserId,
            DateTime createdAtUtc, bool mustChangePassword = true)
        {

            if (personId <= 0)
            {
                throw new DomainException("الحساب يجب أن يرتبط بشخص..");
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new DomainException("اسم المستخدم لا يمكن أن يكون فارغًا.");
            }
            if ((username.Trim()).Length < 3)
            {
                throw new DomainException("اسم المستخدم يجب أن يكون من 3 أحرف على الأقل.");
            }
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new DomainException("كلمة المرور لا يمكن أن تكون فارغة.");
            }


            return new UserAccount(
                personId,
                username.Trim(),
                passwordHash,
                isActive: true,
                0, // FailedLoginCount
                null, // LastLoginAtUtc
                mustChangePassword, 
                createdAtUtc,
                createdByUserId,
                null, // UpdatedAtUtc
                null // UpdatedByUserId
            );
        }


        
        public static UserAccount Load(int id,int personId, string username, string passwordHash, bool isActive, int failedLoginCount,
            DateTime? lastLoginAtUtc, bool mustChangePassword, DateTime createdAtUtc, int? createdByUserId,
            DateTime? updatedAtUtc, int? updatedByUserId)
        {
            return new UserAccount(id, personId, username, passwordHash, isActive, failedLoginCount,
                lastLoginAtUtc, mustChangePassword, createdAtUtc, createdByUserId,
                updatedAtUtc, updatedByUserId);

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

        public void RegisterSuccessfulLogin(DateTime utcNow)
        {
            FailedLoginCount = 0;
            LastLoginAtUtc = utcNow;
        }

        public void RegisterFailedLogin()
        {
            FailedLoginCount++;
        }

        public void ChangePasswordHash(string newHash)
        {
            if (string.IsNullOrWhiteSpace(newHash))
            {
                throw new DomainException("كلمة المرور الجديدة لا يمكن أن تكون فارغة.");
            }
            PasswordHash = newHash;
            FailedLoginCount = 0;
            MustChangePassword = false;

        }





    }
}
