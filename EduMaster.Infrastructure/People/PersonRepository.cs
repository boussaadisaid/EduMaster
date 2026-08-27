using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.People;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using EduMaster.Infrastructure.Persistence;


namespace EduMaster.Infrastructure.People;

public sealed class PersonRepository : IPersonRepository
{
    private readonly IAdoDbSession _session;

    public PersonRepository(IAdoDbSession session) => _session = session;

    private sealed record PersonRow(
        int Id,
        string FirstName,
        string LastName,
        string? FatherName,
        DateTime? BirthDate,
        byte? Gender,
        string? Phone,
        string? Phone2,
        string? Email,
        string? Address,
        string? PhotoPath,
        string? FullNameNormalized,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private const string SelectSql = @"
SELECT Id, FirstName, LastName, FatherName, BirthDate, Gender, Phone, Phone2,
       Email, Address, PhotoPath, FullNameNormalized, IsActive,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Persons";

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Persons
    (FirstName, LastName, FatherName, BirthDate, Gender, Phone, Phone2,
     Email, Address, PhotoPath, FullNameNormalized, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES
    (@FirstName, @LastName, @FatherName, @BirthDate, @Gender, @Phone, @Phone2,
     @Email, @Address, @PhotoPath, @FullNameNormalized, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                FirstName = person.FirstName.Value,
                LastName = person.LastName.Value,
                FatherName = person.FatherName?.Value,
                BirthDate = person.BirthDate?.Value.ToDateTime(TimeOnly.MinValue),          // DateOnly? مباشرة — عمود DATE
                Gender = (byte?)person.Gender,
                Phone = person.Phone?.Value,
                Phone2 = person.Phone2?.Value,
                Email = person.Email?.Value,
                person.Address,
                person.PhotoPath,
                person.FullNameNormalized,                     // الكيان حسبها بالمطبِّع المشترك
                person.IsActive,
                person.CreatedAtUtc,
                person.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        person.SetId(newId);
    }

    public async Task UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE Persons
SET FirstName          = @FirstName,
    LastName           = @LastName,
    FatherName         = @FatherName,
    BirthDate          = @BirthDate,
    Gender             = @Gender,
    Phone              = @Phone,
    Phone2             = @Phone2,
    Email              = @Email,
    Address            = @Address,
    PhotoPath          = @PhotoPath,
    FullNameNormalized = @FullNameNormalized,
    IsActive           = @IsActive,
    UpdatedAtUtc       = @UpdatedAtUtc,
    UpdatedByUserId    = @UpdatedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                FirstName = person.FirstName.Value,
                LastName = person.LastName.Value,
                FatherName = person.FatherName?.Value,
                BirthDate = person.BirthDate?.Value.ToDateTime(TimeOnly.MinValue),
                Gender = (byte?)person.Gender,
                Phone = person.Phone?.Value,
                Phone2 = person.Phone2?.Value,
                Email = person.Email?.Value,
                person.Address,
                person.PhotoPath,
                person.FullNameNormalized,
                person.IsActive,
                person.UpdatedAtUtc,
                person.UpdatedByUserId,
                person.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Person {person.Id} was not found for update.");
    }

    public async Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<PersonRow>(
            new CommandDefinition(SelectSql + "\nWHERE Id = @Id AND IsDeleted = 0;",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IEnumerable<Person>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // المصطلح يصل مطبَّعاً من الـHandler — نفس دالة الكتابة (ح-3)
        const string whereSql = @"
WHERE IsDeleted = 0
  AND (@Term IS NULL
       OR FullNameNormalized LIKE '%' + @Term + '%'
       OR Phone LIKE '%' + @Term + '%'
       OR Phone2 LIKE '%' + @Term + '%')
ORDER BY FirstName, LastName;";

        var rows = await connection.QueryAsync<PersonRow>(
            new CommandDefinition(SelectSql + whereSql,
                new { Term = string.IsNullOrWhiteSpace(normalizedTerm) ? null : normalizedTerm },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToDomain);
    }

    // ⚠ D-81: بنفس ترتيب الاستعلام (6.6 — ز-2)
    private sealed record PersonDuplicateRow(int Id, string FirstName, string LastName);

    public async Task<PersonDuplicateRaw?> GetByNormalizedFullNameAsync(string normalizedFullName, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // 6.6 — ز-2: تطابق تام على الاسم المطبَّع المخزَّن (الكيان يعيد حسابه عند كل كتابة — الشفاء الذاتي)
        const string sqlDuplicate = @"
SELECT TOP 1 Id, FirstName, LastName
FROM Persons
WHERE IsDeleted = 0 AND FullNameNormalized = @Name
ORDER BY Id;";

        var row = await connection.QueryFirstOrDefaultAsync<PersonDuplicateRow>(
            new CommandDefinition(sqlDuplicate, new { Name = normalizedFullName },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : new PersonDuplicateRaw(row.Id, $"{row.FirstName} {row.LastName}");
    }

    private static Person MapToDomain(PersonRow row) =>
        Person.Load(
            id: row.Id,
            firstName: new FirstName(row.FirstName),
            lastName: new LastName(row.LastName),
            fatherName: row.FatherName is null ? null : new FirstName(row.FatherName),
            birthDate: row.BirthDate is null ? null : BirthDate.Load(DateOnly.FromDateTime((row.BirthDate.Value))),
            gender: (GenderType?)row.Gender,
            phone: row.Phone is null ? null : new Phone(row.Phone),
            phone2: row.Phone2 is null ? null : new Phone(row.Phone2),
            email: row.Email is null ? null : new Email(row.Email),
            address: row.Address,
            photoPath: row.PhotoPath,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}