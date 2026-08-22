using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Academic;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Academic;

public sealed class RoomRepository : IRoomRepository
{
    private readonly IAdoDbSession _session;

    public RoomRepository(IAdoDbSession session) => _session = session;

    private sealed record RoomRow(
        int Id,
        string Name,
        int? Capacity,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private const string SelectColumns = @"
SELECT Id, Name, Capacity, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Rooms";

    public async Task AddAsync(Room room, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Rooms (Name, Capacity, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@Name, @Capacity, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                room.Name,
                room.Capacity,
                room.IsActive,
                room.CreatedAtUtc,
                room.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        room.SetId(newId);
    }

    public async Task UpdateAsync(Room room, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Rooms
SET Name            = @Name,
    Capacity        = @Capacity,
    IsActive        = @IsActive,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                room.Name,
                room.Capacity,
                room.IsActive,
                room.UpdatedAtUtc,
                room.UpdatedByUserId,
                room.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Room {room.Id} was not found for update.");
    }

    public async Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<RoomRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<RoomRow>(
            new CommandDefinition($"{SelectColumns} ORDER BY Name;",
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToDomain).ToList();
    }

    public async Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Rooms WHERE Name = @Name AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { Name = name, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-55 (مفعَّل منذ 2.4): أفواج فعّالة مسندة لهذه القاعة (الاختيارية دائماً — D-44) تمنع تعطيلها — الفارغ لا يطابق أبداً
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM ClassGroups WHERE RoomId = @Id AND IsActive = 1;",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    private static Room MapToDomain(RoomRow row) =>
        Room.Load(
            id: row.Id,
            name: row.Name,
            capacity: row.Capacity,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}