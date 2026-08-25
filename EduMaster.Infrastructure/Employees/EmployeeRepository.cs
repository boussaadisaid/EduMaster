using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Employees;
using EduMaster.Domain.Employees;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Employees;

/// <summary>مستودع الموظفين — مرآة TeacherRepository حرفاً (D-115)</summary>
public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly IAdoDbSession _session;

    public EmployeeRepository(IAdoDbSession session) => _session = session;

    private sealed record EmployeeRow(
        int Id,
        int PersonId,
        string JobTitle,
        string? Notes,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Employees (PersonId, JobTitle, Notes, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@PersonId, @JobTitle, @Notes, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                employee.PersonId,
                employee.JobTitle,
                employee.Notes,
                employee.CreatedAtUtc,
                employee.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        employee.SetId(newId);
    }

    public async Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Employees
SET JobTitle        = @JobTitle,
    Notes           = @Notes,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                employee.JobTitle,
                employee.Notes,
                employee.UpdatedAtUtc,
                employee.UpdatedByUserId,
                employee.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Employee {employee.Id} was not found for update.");
    }

    public async Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PersonId, JobTitle, Notes,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Employees
WHERE Id = @Id AND IsDeleted = 0;";

        var row = await connection.QuerySingleOrDefaultAsync<EmployeeRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM Employees WHERE PersonId = @PersonId AND IsDeleted = 0;",
                new { PersonId = personId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IEnumerable<EmployeeListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح — والبحث يشمل الوظيفة أيضاً (طبيعي للموظفين: «محاسبة»)
        const string sql = @"
SELECT e.Id, e.PersonId, e.JobTitle, e.Notes,
       p.FirstName, p.FatherName, p.LastName, p.BirthDate, p.Gender,
       p.Phone, p.Phone2, p.Email, p.Address, p.PhotoPath, p.IsActive
FROM Employees e
JOIN Persons p ON p.Id = e.PersonId AND p.IsDeleted = 0
WHERE e.IsDeleted = 0
  AND (@Term IS NULL
       OR p.FullNameNormalized LIKE '%' + @Term + '%'
       OR p.Phone LIKE '%' + @Term + '%'
       OR p.Phone2 LIKE '%' + @Term + '%'
       OR e.JobTitle LIKE '%' + @Term + '%')
ORDER BY p.FirstName, p.LastName;";

        var rows = await connection.QueryAsync<EmployeeListRow>(
            new CommandDefinition(sql,
                new { Term = string.IsNullOrWhiteSpace(normalizedTerm) ? null : normalizedTerm },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new EmployeeListItem(
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
            row.JobTitle,
            row.Notes,
            row.IsActive));
    }

    public async Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // بروح D-109: سياسة أجر أو يوم عمل مسجَّل على الملف يمنع إزالته (وتُضاف أرصدة 5.3 هنا لاحقاً)
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(@"
SELECT (SELECT COUNT(*) FROM PayPolicies WHERE EmployeeId = @Id)
     + (SELECT COUNT(*) FROM EmployeeWorkLog WHERE EmployeeId = @Id);",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Employees
SET IsDeleted = 1, UpdatedAtUtc = @DeletedAtUtc, UpdatedByUserId = @DeletedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { DeletedAtUtc = deletedAtUtc, DeletedByUserId = deletedByUserId, Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Employee {id} was not found for soft delete.");
    }

    // ⚠ D-81: بترتيب أعمدة SELECT في SearchAsync حرفياً
    private sealed record EmployeeListRow(
        int Id,
        int PersonId,
        string JobTitle,
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

    private static Employee MapToDomain(EmployeeRow row) =>
        Employee.Load(
            id: row.Id,
            personId: row.PersonId,
            jobTitle: row.JobTitle,
            notes: row.Notes,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}