using EduMaster.Application.Treasury;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ITreasuryReadRepository
{
    Task<IReadOnlyList<TreasuryMovementItem>> GetMovementsAsync(
        int? treasuryAccountId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<TreasurySummaryItem> GetSummaryAsync(
        int? treasuryAccountId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
