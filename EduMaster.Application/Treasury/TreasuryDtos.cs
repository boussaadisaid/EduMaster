using EduMaster.Domain.Treasury;

namespace EduMaster.Application.Treasury;

public sealed record TreasuryAccountItem(int Id, string Name, bool IsActive, long OpeningBalanceCentimes);

public sealed record TreasuryMovementItem(
    int SourceKind,
    int SourceId,
    int TreasuryAccountId,
    string AccountName,
    DateOnly MovementDate,
    string Description,
    long IncomingCentimes,
    long OutgoingCentimes,
    long DeltaCentimes,
    TreasuryTransactionKind? TransactionKind,
    string? Note,
    bool CanEdit,
    bool CanDelete);

public sealed record TreasurySummaryItem(
    long OpeningBalanceCentimes,
    long PeriodIncomingCentimes,
    long PeriodOutgoingCentimes,
    long PeriodNetCentimes,
    long ClosingBalanceCentimes);
