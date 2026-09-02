using EduMaster.Domain.Treasury;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ITreasuryTransactionRepository
{
    Task AddAsync(TreasuryTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(TreasuryTransaction transaction, CancellationToken cancellationToken = default);
    Task<TreasuryTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default);
}
