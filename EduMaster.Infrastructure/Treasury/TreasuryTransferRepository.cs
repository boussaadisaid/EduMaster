
using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Treasury;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Treasury;

public sealed class TreasuryTransferRepository : ITreasuryTransferRepository
{
    private readonly IAdoDbSession _session;
    public TreasuryTransferRepository(IAdoDbSession session) => _session = session;
    private sealed record Row(int Id, int FromTreasuryAccountId, int ToTreasuryAccountId, DateTime TransferDate, long AmountCentimes, string? Note, bool IsDeleted, DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? DeletedAtUtc, int? DeletedByUserId);
    public async Task AddAsync(TreasuryTransfer transfer, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO dbo.TreasuryTransfers(FromTreasuryAccountId,ToTreasuryAccountId,TransferDate,AmountCentimes,Note,CreatedAtUtc,CreatedByUserId) OUTPUT INSERTED.Id VALUES(@FromTreasuryAccountId,@ToTreasuryAccountId,@TransferDate,@AmountCentimes,@Note,@CreatedAtUtc,@CreatedByUserId);", new { transfer.FromTreasuryAccountId, transfer.ToTreasuryAccountId, TransferDate = transfer.TransferDate.ToDateTime(TimeOnly.MinValue), transfer.AmountCentimes, transfer.Note, transfer.CreatedAtUtc, transfer.CreatedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        transfer.SetId(id);
    }
    public async Task<TreasuryTransfer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var r = await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT Id,FromTreasuryAccountId,ToTreasuryAccountId,TransferDate,AmountCentimes,Note,IsDeleted,CreatedAtUtc,CreatedByUserId,DeletedAtUtc,DeletedByUserId FROM dbo.TreasuryTransfers WHERE Id=@Id;", new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return r is null ? null : TreasuryTransfer.Load(r.Id, r.FromTreasuryAccountId, r.ToTreasuryAccountId, DateOnly.FromDateTime(r.TransferDate), r.AmountCentimes, r.Note, r.IsDeleted, r.CreatedAtUtc, r.CreatedByUserId, r.DeletedAtUtc, r.DeletedByUserId);
    }
    public async Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var n = await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.TreasuryTransfers SET IsDeleted=1,DeletedAtUtc=@DeletedAtUtc,DeletedByUserId=@DeletedByUserId WHERE Id=@Id AND IsDeleted=0;", new { Id = id, DeletedAtUtc = deletedAtUtc, DeletedByUserId = deletedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (n == 0) throw new InvalidOperationException($"TreasuryTransfer {id} was not found for soft delete.");
    }
}
