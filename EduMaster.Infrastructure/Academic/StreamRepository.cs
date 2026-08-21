using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Infrastructure.Persistence;



namespace EduMaster.Infrastructure.Academic;

public sealed class StreamRepository : IStreamRepository
{
    private readonly IAdoDbSession _session;

    public StreamRepository(IAdoDbSession session) => _session = session;

    private sealed record StreamRow(
        int Id,
        int LevelId,
        string Name,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private const string SelectColumns = @"
SELECT Id, LevelId, Name, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Streams";

    public async Task AddAsync(Domain.Academic.Stream stream, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Streams (LevelId, Name, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@LevelId, @Name, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                stream.LevelId,
                stream.Name,
                stream.IsActive,
                stream.CreatedAtUtc,
                stream.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        stream.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Academic.Stream stream, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Streams
SET Name            = @Name,
    IsActive        = @IsActive,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                stream.Name,
                stream.IsActive,
                stream.UpdatedAtUtc,
                stream.UpdatedByUserId,
                stream.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Stream {stream.Id} was not found for update.");
    }

    public async Task<Domain.Academic.Stream?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<StreamRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<Domain.Academic.Stream>> GetByLevelIdAsync(int levelId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<StreamRow>(
            new CommandDefinition($"{SelectColumns} WHERE LevelId = @LevelId ORDER BY Name;", new { LevelId = levelId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToDomain).ToList();
    }

    public async Task<bool> AnyWithNameInLevelAsync(int levelId, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"SELECT COUNT(*) FROM Streams
                  WHERE LevelId = @LevelId AND Name = @Name AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { LevelId = levelId, Name = name, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        // F2: حين تُضاف الأفواج (تشير إلى الشعب) تُفحص هنا — اليوم لا جداول تشير إلى Streams
        return Task.FromResult(false);
    }

    private static Domain.Academic.Stream MapToDomain(StreamRow row) =>
        Domain.Academic.Stream.Load(
            id: row.Id,
            levelId: row.LevelId,
            name: row.Name,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}