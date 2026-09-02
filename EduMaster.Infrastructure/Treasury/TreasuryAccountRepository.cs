
using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Treasury;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Treasury;

public sealed class TreasuryAccountRepository : ITreasuryAccountRepository
{
    private readonly IAdoDbSession _session;
    public TreasuryAccountRepository(IAdoDbSession session) => _session = session;
    private sealed record Row(int Id, string Name, bool IsActive, long OpeningBalanceCentimes,
        DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? UpdatedAtUtc, int? UpdatedByUserId);
    private const string Columns = "Id, Name, IsActive, OpeningBalanceCentimes, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId";

    public async Task AddAsync(TreasuryAccount account, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"INSERT INTO dbo.TreasuryAccounts(Name,IsActive,OpeningBalanceCentimes,CreatedAtUtc,CreatedByUserId)
OUTPUT INSERTED.Id VALUES(@Name,@IsActive,@OpeningBalanceCentimes,@CreatedAtUtc,@CreatedByUserId);";
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { account.Name, account.IsActive, account.OpeningBalanceCentimes, account.CreatedAtUtc, account.CreatedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        account.SetId(id);
    }
    public async Task UpdateAsync(TreasuryAccount account, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"UPDATE dbo.TreasuryAccounts SET Name=@Name,IsActive=@IsActive,OpeningBalanceCentimes=@OpeningBalanceCentimes,UpdatedAtUtc=@UpdatedAtUtc,UpdatedByUserId=@UpdatedByUserId WHERE Id=@Id;", new { account.Name, account.IsActive, account.OpeningBalanceCentimes, account.UpdatedAtUtc, account.UpdatedByUserId, account.Id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"TreasuryAccount {account.Id} was not found for update.");
    }
    public async Task<TreasuryAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition($"SELECT {Columns} FROM dbo.TreasuryAccounts WHERE Id=@Id;", new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return row is null ? null : TreasuryAccount.Load(row.Id, row.Name, row.IsActive, row.OpeningBalanceCentimes, row.CreatedAtUtc, row.CreatedByUserId, row.UpdatedAtUtc, row.UpdatedByUserId);
    }
    public async Task<IReadOnlyList<TreasuryAccount>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition($"SELECT {Columns} FROM dbo.TreasuryAccounts WHERE (@ActiveOnly=0 OR IsActive=1) ORDER BY Name;", new { ActiveOnly = activeOnly }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.Select(r => TreasuryAccount.Load(r.Id, r.Name, r.IsActive, r.OpeningBalanceCentimes, r.CreatedAtUtc, r.CreatedByUserId, r.UpdatedAtUtc, r.UpdatedByUserId)).ToList();
    }
    public async Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM dbo.TreasuryAccounts WHERE Name=@Name AND (@ExcludeId IS NULL OR Id<>@ExcludeId);", new { Name = name.Trim(), ExcludeId = excludeId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return count > 0;
    }
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM dbo.TreasuryAccounts WHERE IsActive=1;", transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
