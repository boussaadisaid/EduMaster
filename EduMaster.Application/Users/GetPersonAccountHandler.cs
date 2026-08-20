using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;




namespace EduMaster.Application.Users
{
    public sealed class GetPersonAccountHandler
    {
        private readonly IUserAccountRepository _accounts;
        private readonly IClock _clock;
        private readonly ILogger<GetPersonAccountHandler> _logger;

        public GetPersonAccountHandler(IUserAccountRepository accounts, IClock clock, ILogger<GetPersonAccountHandler> logger)
        {
            _accounts = accounts;
            _clock = clock;
            _logger = logger;
        }

        public async Task<OperationResult<PersonAccountInfo?>> ExecuteAsync(int personId, CancellationToken cancellationToken = default)
        {
            try
            {
                var account = await _accounts.GetByPersonIdAsync(personId, cancellationToken);
                if (account is null)
                    return OperationResult<PersonAccountInfo?>.Success(null);   // لا حساب — ليست خطأ

                var locked = account.IsLockedOut(_clock.UtcNow);
                var info = new PersonAccountInfo(
                    account.Id,
                    account.Username,
                    account.IsActive,
                    locked,
                    locked ? (int)Math.Ceiling(account.RemainingLockout(_clock.UtcNow)!.Value.TotalMinutes) : null);

                return OperationResult<PersonAccountInfo?>.Success(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load account for person {PersonId}", personId);
                return OperationResult<PersonAccountInfo?>.Failure("حدث خطأ غير متوقع أثناء تحميل الحساب.", ErrorType.Unexpected);
            }
        }
    }
}
