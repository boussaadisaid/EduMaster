using EduMaster.Domain.Treasury;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ITreasuryAccountRepository
{
    Task AddAsync(TreasuryAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(TreasuryAccount account, CancellationToken cancellationToken = default);
    Task<TreasuryAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreasuryAccount>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);
}
