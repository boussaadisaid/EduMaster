using EduMaster.Domain.Treasury;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ITreasuryTransferRepository
{
    Task AddAsync(TreasuryTransfer transfer, CancellationToken cancellationToken = default);
    Task<TreasuryTransfer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default);
}
