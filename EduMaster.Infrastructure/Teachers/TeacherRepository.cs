using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Teachers;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Teachers;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Teachers;

public sealed class TeacherRepository : ITeacherRepository
{
    private readonly IAdoDbSession _session;

    public TeacherRepository(IAdoDbSession session) => _session = session;

    private sealed record TeacherRow(
        int Id,
        int PersonId,
        string? Specialty,
        string? Notes,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    public async Task AddAsync(Teacher teacher, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Teachers (PersonId, Specialty, Notes, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@PersonId, @Specialty, @Notes, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                teacher.PersonId,
                teacher.Specialty,
                teacher.Notes,
                teacher.CreatedAtUtc,
                teacher.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        teacher.SetId(newId);
    }

    public async Task UpdateAsync(Teacher teacher, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Teachers
SET Specialty       = @Specialty,
    Notes           = @Notes,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                teacher.Specialty,
                teacher.Notes,
                teacher.UpdatedAtUtc,
                teacher.UpdatedByUserId,
                teacher.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Teacher {teacher.Id} was not found for update.");
    }

    public async Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PersonId, Specialty, Notes,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Teachers
WHERE Id = @Id AND IsDeleted = 0;";

        var row = await connection.QuerySingleOrDefaultAsync<TeacherRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM Teachers WHERE PersonId = @PersonId AND IsDeleted = 0;",
                new { PersonId = personId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IEnumerable<TeacherListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح — والبحث يشمل التخصص أيضاً (طبيعي للأساتذة: «رياضيات»)
        const string sql = @"
SELECT t.Id, t.PersonId, t.Specialty, t.Notes,
       p.FirstName, p.FatherName, p.LastName, p.BirthDate, p.Gender,
       p.Phone, p.Phone2, p.Email, p.Address, p.PhotoPath, p.IsActive
FROM Teachers t
JOIN Persons p ON p.Id = t.PersonId AND p.IsDeleted = 0
WHERE t.IsDeleted = 0
  AND (@Term IS NULL
       OR p.FullNameNormalized LIKE '%' + @Term + '%'
       OR p.Phone LIKE '%' + @Term + '%'
       OR p.Phone2 LIKE '%' + @Term + '%'
       OR t.Specialty LIKE '%' + @Term + '%')
ORDER BY p.FirstName, p.LastName;";

        var rows = await connection.QueryAsync<TeacherListRow>(
            new CommandDefinition(sql,
                new { Term = string.IsNullOrWhiteSpace(normalizedTerm) ? null : normalizedTerm },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new TeacherListItem(
            row.Id,
            row.PersonId,
            row.FirstName,
            row.LastName,
            row.FatherName,
            row.BirthDate is null
                ? null
                : DateOnly.FromDateTime(row.BirthDate.Value),
            (GenderType?)row.Gender,
            row.Phone,
            row.Phone2,
            row.Email,
            row.Address,
            row.PhotoPath,
            row.Specialty,
            row.Notes,
            row.IsActive));
    }

    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        // F2/F5: حين تُضاف التسجيلات/المستحقات تُفحص هنا — اليوم لا جداول تشير إلى Teachers
        return Task.FromResult(false);
    }

    public async Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Teachers
SET IsDeleted = 1, UpdatedAtUtc = @DeletedAtUtc, UpdatedByUserId = @DeletedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { DeletedAtUtc = deletedAtUtc, DeletedByUserId = deletedByUserId, Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Teacher {id} was not found for soft delete.");
    }

    private sealed record TeacherListRow(
        int Id,
        int PersonId,
        string? Specialty,
        string? Notes,
        string FirstName,
        string? FatherName,
        string LastName,
        DateTime? BirthDate,
        byte? Gender,
        string? Phone,
        string? Phone2,
        string? Email,
        string? Address,
        string? PhotoPath,
        bool IsActive);

    private static Teacher MapToDomain(TeacherRow row) =>
        Teacher.Load(
            id: row.Id,
            personId: row.PersonId,
            specialty: row.Specialty,
            notes: row.Notes,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}