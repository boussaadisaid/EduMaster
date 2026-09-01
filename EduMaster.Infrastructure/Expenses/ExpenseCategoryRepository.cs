using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Expenses;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Expenses;

public sealed class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private readonly IAdoDbSession _session;
    public ExpenseCategoryRepository(IAdoDbSession session) => _session = session;

    private sealed record Row(int Id, string Name, bool IsActive, DateTime CreatedAtUtc, int? CreatedByUserId,
        DateTime? UpdatedAtUtc, int? UpdatedByUserId);
    private const string Columns = "Id, Name, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId";

    public async Task AddAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"INSERT INTO dbo.ExpenseCategories (Name, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id VALUES (@Name, @IsActive, @CreatedAtUtc, @CreatedByUserId);";
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            category.Name,
            category.IsActive,
            category.CreatedAtUtc,
            category.CreatedByUserId
        }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        category.SetId(id);
    }

    public async Task UpdateAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"UPDATE dbo.ExpenseCategories
SET Name=@Name, IsActive=@IsActive, UpdatedAtUtc=@UpdatedAtUtc, UpdatedByUserId=@UpdatedByUserId
WHERE Id=@Id;";
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            category.Name,
            category.IsActive,
            category.UpdatedAtUtc,
            category.UpdatedByUserId,
            category.Id
        }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"ExpenseCategory {category.Id} was not found for update.");
    }

    public async Task<ExpenseCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.ExpenseCategories WHERE Id=@Id;", new { Id = id },
            transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<ExpenseCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.ExpenseCategories ORDER BY Name;",
            transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.Select(Map).ToList();
    }

    public async Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.ExpenseCategories WHERE Name=@Name AND (@ExcludeId IS NULL OR Id<>@ExcludeId);",
            new { Name = name, ExcludeId = excludeId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return count > 0;
    }

    private static ExpenseCategory Map(Row row) => ExpenseCategory.Load(row.Id, row.Name, row.IsActive,
        row.CreatedAtUtc, row.CreatedByUserId, row.UpdatedAtUtc, row.UpdatedByUserId);
}
