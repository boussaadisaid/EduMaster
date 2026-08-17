using EduMaster.Domain.Users;



namespace EduMaster.Application.Abstractions.Repositories
{
    public interface IUserAccountRepository
    {
        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken = default);

       // public Task<UserAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        public Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

       // public Task<bool> ChangePasswordAsync(int userAccountId, string newPassword, CancellationToken cancellationToken = default);

        Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default);
        Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default);

    }
}
