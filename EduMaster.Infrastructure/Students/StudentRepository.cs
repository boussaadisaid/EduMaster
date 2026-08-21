using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Students;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Students;

public sealed class StudentRepository : IStudentRepository
{
    private readonly IAdoDbSession _session;

    public StudentRepository(IAdoDbSession session) => _session = session;

    private sealed record StudentRow(
        int Id,
        int PersonId,
        int? GuardianPersonId,
        byte Category,
        string? Notes,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    public async Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Students (PersonId, GuardianPersonId, Category, Notes, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@PersonId, @GuardianPersonId, @Category, @Notes, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                student.PersonId,
                student.GuardianPersonId,
                Category = (byte)student.Category,
                student.Notes,
                student.CreatedAtUtc,
                student.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        student.SetId(newId);
    }

    public async Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Students
SET GuardianPersonId = @GuardianPersonId,
    Category         = @Category,
    Notes            = @Notes,
    UpdatedAtUtc     = @UpdatedAtUtc,
    UpdatedByUserId  = @UpdatedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                student.GuardianPersonId,
                Category = (byte)student.Category,
                student.Notes,
                student.UpdatedAtUtc,
                student.UpdatedByUserId,
                student.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Student {student.Id} was not found for update.");
    }

    public async Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PersonId, GuardianPersonId, Category, Notes,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Students
WHERE Id = @Id AND IsDeleted = 0;";

        var row = await connection.QuerySingleOrDefaultAsync<StudentRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM Students WHERE PersonId = @PersonId AND IsDeleted = 0;",
                new { PersonId = personId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IEnumerable<StudentListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح: الملف + النواة كاملة (لملء المحرر) + اسم ولي الأمر — والبحث يشمله أيضاً
        const string sql = @"
SELECT s.Id, s.PersonId, s.Category, s.Notes, s.GuardianPersonId,
       p.FirstName, p.FatherName, p.LastName, p.BirthDate, p.Gender,
       p.Phone, p.Phone2, p.Email, p.Address, p.PhotoPath, p.IsActive,
       g.FirstName AS GuardianFirstName, g.LastName AS GuardianLastName
FROM Students s
JOIN Persons p ON p.Id = s.PersonId AND p.IsDeleted = 0
LEFT JOIN Persons g ON g.Id = s.GuardianPersonId AND g.IsDeleted = 0
WHERE s.IsDeleted = 0
  AND (@Term IS NULL
       OR p.FullNameNormalized LIKE '%' + @Term + '%'
       OR p.Phone LIKE '%' + @Term + '%'
       OR p.Phone2 LIKE '%' + @Term + '%'
       OR g.FullNameNormalized LIKE '%' + @Term + '%')
ORDER BY p.FirstName, p.LastName;";

        var rows = await connection.QueryAsync<StudentListRow>(
            new CommandDefinition(sql,
                new { Term = string.IsNullOrWhiteSpace(normalizedTerm) ? null : normalizedTerm },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new StudentListItem(
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
            (StudentCategory)row.Category,
            row.GuardianPersonId,
            row.GuardianFirstName is null ? null : $"{row.GuardianFirstName} {row.GuardianLastName}",
            row.Notes,
            row.IsActive));
    }

    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        // F2: حين تُضاف التسجيلات تُفحص هنا — اليوم لا جداول تشير إلى Students
        return Task.FromResult(false);
    }

    public async Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Students
SET IsDeleted = 1, UpdatedAtUtc = @DeletedAtUtc, UpdatedByUserId = @DeletedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { DeletedAtUtc = deletedAtUtc, DeletedByUserId = deletedByUserId, Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Student {id} was not found for soft delete.");
    }

    private sealed record StudentListRow(
        int Id,
        int PersonId,
        byte Category,
        string? Notes,
        int? GuardianPersonId,
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
        bool IsActive,
        string? GuardianFirstName,
        string? GuardianLastName);

    private static Student MapToDomain(StudentRow row) =>
        Student.Load(
            id: row.Id,
            personId: row.PersonId,
            guardianPersonId: row.GuardianPersonId,
            category: (StudentCategory)row.Category,
            notes: row.Notes,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}