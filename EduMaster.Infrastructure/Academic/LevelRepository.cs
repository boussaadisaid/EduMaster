using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Academic;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Academic;

public sealed class LevelRepository : ILevelRepository
{
    private readonly IAdoDbSession _session;

    public LevelRepository(IAdoDbSession session) => _session = session;

    private sealed record LevelRow(
        int Id,
        string Name,
        int SortOrder,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private const string SelectColumns = @"
SELECT Id, Name, SortOrder, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Levels";

    public async Task AddAsync(Level level, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Levels (Name, SortOrder, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@Name, @SortOrder, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                level.Name,
                level.SortOrder,
                level.IsActive,
                level.CreatedAtUtc,
                level.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        level.SetId(newId);
    }

    public async Task UpdateAsync(Level level, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // تحديث واحد يغطي التعديل والتعطيل/التفعيل معاً (IsActive ضمنه)
        const string sql = @"
UPDATE Levels
SET Name            = @Name,
    SortOrder       = @SortOrder,
    IsActive        = @IsActive,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                level.Name,
                level.SortOrder,
                level.IsActive,
                level.UpdatedAtUtc,
                level.UpdatedByUserId,
                level.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Level {level.Id} was not found for update.");
    }

    public async Task<Level?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<LevelRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<Level>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<LevelRow>(
            new CommandDefinition($"{SelectColumns} ORDER BY SortOrder, Name;",
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToDomain).ToList();
    }

    public async Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Levels WHERE Name = @Name AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { Name = name, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-55 (مفعَّل منذ 2.4): أفواج فعّالة على هذا المستوى تمنع تعطيله — المعطّلة تاريخ فلا تمنع
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM ClassGroups WHERE LevelId = @Id AND IsActive = 1;",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    private static Level MapToDomain(LevelRow row) =>
        Level.Load(
            id: row.Id,
            name: row.Name,
            sortOrder: row.SortOrder,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}