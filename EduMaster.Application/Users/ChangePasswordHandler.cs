using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.Users
{
    public sealed class ChangePasswordHandler
    {
        // الاعتماديات: IUserAccountRepository + IPasswordHasher + IClock + ICurrentUserService + IUnitOfWork + ILogger
        private readonly IUserAccountRepository _accounts;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _hasher;
        private readonly ILogger<ChangePasswordHandler> _logger;

        public ChangePasswordHandler(IUserAccountRepository accounts,
             IClock clock, ICurrentUserService currentUser,
            IUnitOfWork unitOfWork, IPasswordHasher hasher, ILogger<ChangePasswordHandler> logger)
        {
            _accounts = accounts;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _hasher = hasher;
            _logger = logger;
        }


        public async Task<OperationResult> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return OperationResult.Failure("أدخل كلمة المرور الجديدة.", ErrorType.Validation);
            if (request.NewPassword.Length < 8)
                return OperationResult.Failure("كلمة المرور الجديدة يجب أن تكون من 8 أحرف على الأقل.", ErrorType.Validation);
            if (request.NewPassword != request.ConfirmPassword)
                return OperationResult.Failure("تأكيد كلمة المرور غير مطابق.", ErrorType.Validation);

            try
            {
                // الجلسة أثبتت الهوية للتو (دخل بالكلمة المؤقتة) — فلا نطلب الكلمة القديمة هنا
                var account = await _accounts.GetByUsernameAsync(_currentUser.Username!, cancellationToken);
                if (account is null)
                {
                    _logger.LogError("Current user account {Username} not found during password change", _currentUser.Username);
                    return OperationResult.Failure("حدث خطأ غير متوقع. أعد تسجيل الدخول.", ErrorType.Unexpected);
                }

                account.ChangePasswordHash(_hasher.Hash(request.NewPassword), _clock.UtcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _accounts.UpdateAsync(account, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

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
                _logger.LogError(ex, "Failed to change password for user {Username}", _currentUser.Username);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تغيير كلمة المرور.", ErrorType.Unexpected);
            }
        }
    }
}
