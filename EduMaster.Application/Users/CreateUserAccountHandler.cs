using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Users;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.Users
{
    public sealed class CreateUserAccountHandler
    {
        private readonly IPersonRepository _persons;
        private readonly IUserAccountRepository _accounts;
        private readonly IPasswordHasher _hasher;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateUserAccountHandler> _logger;

        public CreateUserAccountHandler(IPersonRepository persons, IUserAccountRepository accounts,
            IPasswordHasher hasher, IClock clock, ICurrentUserService currentUser, 
            IUnitOfWork unitOfWork, ILogger<CreateUserAccountHandler> logger)
        {
            _persons = persons;
            _accounts = accounts;
            _hasher = hasher;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OperationResult> ExecuteAsync(CreateUserAccountRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Username))
                return OperationResult.Failure("أدخل اسم المستخدم.", ErrorType.Validation);
            if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
                return OperationResult.Failure("أدخل كلمة المرور المؤقتة.", ErrorType.Validation);
            if (request.TemporaryPassword.Length < 8)
                return OperationResult.Failure("كلمة المرور المؤقتة يجب أن تكون من 8 أحرف على الأقل.", ErrorType.Validation);

            try
            {
                var person = await _persons.GetByIdAsync(request.PersonId, cancellationToken);
                if (person is null)
                    return OperationResult.Failure("الشخص غير موجود.", ErrorType.NotFound);

                if (!person.IsActive)
                    return OperationResult.Failure("لا يمكن إنشاء حساب لشخص معطّل — فعّله أولاً.", ErrorType.BusinessRule);

                var existing = await _accounts.GetByPersonIdAsync(request.PersonId, cancellationToken);
                if (existing is not null)
                    return OperationResult.Failure($"لهذا الشخص حساب بالفعل: {existing.Username}", ErrorType.Conflict);

                if (await _accounts.AnyWithUsernameAsync(request.Username.Trim(), cancellationToken))
                    return OperationResult.Failure("اسم المستخدم محجوز — اختر اسماً آخر.", ErrorType.Conflict);

                var hash = _hasher.Hash(request.TemporaryPassword);   // ⚠️ طابق اسم الأسلوب في واجهتك

                var account = UserAccount.Create(
                    personId: request.PersonId,
                    username: request.Username,
                    passwordHash: hash,
                    createdAtUtc: _clock.UtcNow,
                    createdByUserId: _currentUser.UserAccountId,
                    mustChangePassword: true);   // كلمة مؤقتة = إلزام التغيير عند أول دخول (ح-5)

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _accounts.AddAsync(account, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Admin {AdminUserId} created login account {Username} for person {PersonId}",
                    _currentUser.UserAccountId, account.Username, request.PersonId);

                return OperationResult.Success();
            }
            catch (DomainException dex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                return OperationResult.Failure(dex.Message, ErrorType.Validation);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to create account for person {PersonId}", request.PersonId);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء إنشاء الحساب.", ErrorType.Unexpected);
            }
        }
    }
}
