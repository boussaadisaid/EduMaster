
using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Treasury;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Treasury;

public sealed class TreasuryTransactionRepository : ITreasuryTransactionRepository
{
    private readonly IAdoDbSession _session;
    public TreasuryTransactionRepository(IAdoDbSession session) => _session = session;
    private sealed record Row(int Id, int TreasuryAccountId, DateTime TransactionDate, byte Kind, long AmountCentimes, string? Note, bool IsDeleted, DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? UpdatedAtUtc, int? UpdatedByUserId, DateTime? DeletedAtUtc, int? DeletedByUserId);
    public async Task AddAsync(TreasuryTransaction transaction, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO dbo.TreasuryTransactions(TreasuryAccountId,TransactionDate,Kind,AmountCentimes,Note,CreatedAtUtc,CreatedByUserId) OUTPUT INSERTED.Id VALUES(@TreasuryAccountId,@TransactionDate,@Kind,@AmountCentimes,@Note,@CreatedAtUtc,@CreatedByUserId);", new { transaction.TreasuryAccountId, TransactionDate = transaction.TransactionDate.ToDateTime(TimeOnly.MinValue), Kind = (byte)transaction.Kind, transaction.AmountCentimes, transaction.Note, transaction.CreatedAtUtc, transaction.CreatedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        transaction.SetId(id);
    }
    public async Task UpdateAsync(TreasuryTransaction transaction, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var n = await c.ExecuteAsync(new CommandDefinition(@"UPDATE dbo.TreasuryTransactions SET TreasuryAccountId=@TreasuryAccountId,TransactionDate=@TransactionDate,Kind=@Kind,AmountCentimes=@AmountCentimes,Note=@Note,UpdatedAtUtc=@UpdatedAtUtc,UpdatedByUserId=@UpdatedByUserId WHERE Id=@Id AND IsDeleted=0;", new { transaction.TreasuryAccountId, TransactionDate = transaction.TransactionDate.ToDateTime(TimeOnly.MinValue), Kind = (byte)transaction.Kind, transaction.AmountCentimes, transaction.Note, transaction.UpdatedAtUtc, transaction.UpdatedByUserId, transaction.Id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (n == 0) throw new InvalidOperationException($"TreasuryTransaction {transaction.Id} was not found for update.");
    }
    public async Task<TreasuryTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var r = await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT Id,TreasuryAccountId,TransactionDate,Kind,AmountCentimes,Note,IsDeleted,CreatedAtUtc,CreatedByUserId,UpdatedAtUtc,UpdatedByUserId,DeletedAtUtc,DeletedByUserId FROM dbo.TreasuryTransactions WHERE Id=@Id;", new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return r is null ? null : TreasuryTransaction.Load(r.Id, r.TreasuryAccountId, DateOnly.FromDateTime(r.TransactionDate), (TreasuryTransactionKind)r.Kind, r.AmountCentimes, r.Note, r.IsDeleted, r.CreatedAtUtc, r.CreatedByUserId, r.UpdatedAtUtc, r.UpdatedByUserId, r.DeletedAtUtc, r.DeletedByUserId);
    }
    public async Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var n = await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.TreasuryTransactions SET IsDeleted=1,DeletedAtUtc=@DeletedAtUtc,DeletedByUserId=@DeletedByUserId WHERE Id=@Id AND IsDeleted=0;", new { Id = id, DeletedAtUtc = deletedAtUtc, DeletedByUserId = deletedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (n == 0) throw new InvalidOperationException($"TreasuryTransaction {id} was not found for soft delete.");
    }
}
