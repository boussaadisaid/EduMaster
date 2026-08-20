using EduMaster.Domain.Users;



namespace EduMaster.Application.Abstractions.Repositories
{
    public interface IUserAccountRepository
    {
        Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken = default);
        Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default);
        Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default);
        Task<UserAccount?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default);
        Task<bool> AnyWithUsernameAsync(string username, CancellationToken cancellationToken = default);

    }
}
