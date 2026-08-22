using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Academic;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Academic;

public sealed class SubjectRepository : ISubjectRepository
{
    private readonly IAdoDbSession _session;

    public SubjectRepository(IAdoDbSession session) => _session = session;

    private sealed record SubjectRow(
        int Id,
        string Name,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private const string SelectColumns = @"
SELECT Id, Name, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Subjects";

    public async Task AddAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Subjects (Name, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@Name, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                subject.Name,
                subject.IsActive,
                subject.CreatedAtUtc,
                subject.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        subject.SetId(newId);
    }

    public async Task UpdateAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Subjects
SET Name            = @Name,
    IsActive        = @IsActive,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                subject.Name,
                subject.IsActive,
                subject.UpdatedAtUtc,
                subject.UpdatedByUserId,
                subject.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Subject {subject.Id} was not found for update.");
    }

    public async Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<SubjectRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<Subject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SubjectRow>(
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
                "SELECT COUNT(*) FROM Subjects WHERE Name = @Name AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { Name = name, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-55 (مفعَّل منذ 2.4): أفواج فعّالة على هذه المادة تمنع تعطيلها — المعطّلة تاريخ فلا تمنع
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM ClassGroups WHERE SubjectId = @Id AND IsActive = 1;",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    private static Subject MapToDomain(SubjectRow row) =>
        Subject.Load(
            id: row.Id,
            name: row.Name,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}