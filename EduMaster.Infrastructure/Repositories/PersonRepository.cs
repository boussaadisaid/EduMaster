using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.People;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.People;

public sealed class PersonRepository : IPersonRepository
{
    private readonly IAdoDbSession _session;

    public PersonRepository(IAdoDbSession session) => _session = session;

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO Persons
                (FirstName, LastName, FatherName, BirthDate, Gender, Phone,
                 Email, Address, PhotoPath, FullNameNormalized, IsActive, CreatedAtUtc, CreatedByUserId)
            OUTPUT INSERTED.Id
            VALUES
                (@FirstName, @LastName, @FatherName, @BirthDate, @Gender, @Phone,
                 @Email, @Address, @PhotoPath, @FullNameNormalized, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                FirstName = person.FirstName.Value,      // ⬅️ الريبوزيتوري يفكّ الـ VO إلى قيمة خام
                LastName = person.LastName.Value,
                FatherName = person.FatherName?.Value,   // VO nullable → قيمة nullable → DBNull تلقائياً
                BirthDate = person.BirthDate?.Value.ToDateTime(TimeOnly.MinValue), // DateOnly → DateTime
                Gender = (byte?)person.Gender,        // enum : byte → byte؟ لعمود TINYINT
                Phone = person.Phone?.Value,
                Email = person.Email?.Value,
                person.Address,
                person.PhotoPath,
                person.FullNameNormalized,               // ⬅️ لا تنسَها — الكيان يحسبها
                person.IsActive,
                person.CreatedAtUtc,
                person.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        person.SetId(newId);
    }
}