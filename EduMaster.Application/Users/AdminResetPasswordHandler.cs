using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;




namespace EduMaster.Application.Users
{
    public sealed class AdminResetPasswordHandler
    {
        // نفس اعتماديات Unlock + IPasswordHasher
        private readonly IUserAccountRepository _accounts;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _hasher;
        private readonly ILogger<AdminResetPasswordHandler> _logger;

        public AdminResetPasswordHandler(IUserAccountRepository accounts,
             IClock clock, ICurrentUserService currentUser,
            IUnitOfWork unitOfWork, IPasswordHasher hasher, ILogger<AdminResetPasswordHandler> logger)
        {
            _accounts = accounts;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _hasher = hasher;
            _logger = logger;
        }
        public async Task<OperationResult> ExecuteAsync(AdminResetPasswordRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.NewTemporaryPassword))
                return OperationResult.Failure("أدخل كلمة المرور المؤقتة الجديدة.", ErrorType.Validation);
            if (request.NewTemporaryPassword.Length < 8)
                return OperationResult.Failure("كلمة المرور المؤقتة يجب أن تكون من 8 أحرف على الأقل.", ErrorType.Validation);

            try
            {
                var account = await _accounts.GetByPersonIdAsync(request.PersonId, cancellationToken);
                if (account is null)
                    return OperationResult.Failure("لا يوجد حساب مرتبط بهذا الشخص.", ErrorType.BusinessRule);

                // إلزام التغيير عند الدخول + فك القفل ضمنياً — سلوك الكيان AdminResetPasswordHash
                account.AdminResetPasswordHash(_hasher.Hash(request.NewTemporaryPassword), _clock.UtcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _accounts.UpdateAsync(account, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Admin {AdminUserId} reset password for account {Username}",
                    _currentUser.UserAccountId, account.Username);

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
                _logger.LogError(ex, "Failed to reset password for person {PersonId}", request.PersonId);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء إعادة تعيين كلمة المرور.", ErrorType.Unexpected);
            }
        }
    }
}
