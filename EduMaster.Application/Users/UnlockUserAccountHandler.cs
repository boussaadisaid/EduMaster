using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.Users
{
    public sealed class UnlockUserAccountHandler
    {
        // الاعتماديات: IUserAccountRepository + IClock + ICurrentUserService + IUnitOfWork + ILogger

        private readonly IUserAccountRepository _accounts;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UnlockUserAccountHandler> _logger;

        public UnlockUserAccountHandler( IUserAccountRepository accounts,
             IClock clock, ICurrentUserService currentUser,
            IUnitOfWork unitOfWork, ILogger<UnlockUserAccountHandler> logger)
        {
            _accounts = accounts;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }


        public async Task<OperationResult> ExecuteAsync(UnlockUserAccountRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var account = await _accounts.GetByPersonIdAsync(request.PersonId, cancellationToken);
                if (account is null)
                    return OperationResult.Failure("لا يوجد حساب مرتبط بهذا الشخص.", ErrorType.BusinessRule);

                if (!account.IsLockedOut(_clock.UtcNow))
                    return OperationResult.Success();   // غير مقفل أصلاً

                account.Unlock(_clock.UtcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _accounts.UpdateAsync(account, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Admin {AdminUserId} unlocked account {Username}",
                    _currentUser.UserAccountId, account.Username);

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to unlock account for person {PersonId}", request.PersonId);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء فك القفل.", ErrorType.Unexpected);
            }
        }
    }
}
