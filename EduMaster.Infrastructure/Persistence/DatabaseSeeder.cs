using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using EduMaster.Domain.Users;

namespace EduMaster.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private readonly IPersonRepository _persons;
    private readonly IUserAccountRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DatabaseSeeder(
        IPersonRepository persons,
        IUserAccountRepository users,
        IPasswordHasher hasher,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _persons = persons;
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _users.AnyUsersAsync(cancellationToken))
            return;   // سبق أن زُرع — التطبيق يعمل بهذا الفحص كل إقلاع، رخيص وسريع

        // ⭐ هنا تصبح المعاملة إلزامية فعلاً: شخص + حساب = كلاهما أو لا شيء
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var person = Person.Create(
                firstName: new FirstName("المدير"),
                lastName: new LastName("العام"),
                fatherName: null, birthDate: null, gender: null,
                phone: null, phone2: null, email: null, address: null, photoPath: null,
                createdAtUtc: _clock.UtcNow,
                createdByUserId: null);       
            await _persons.AddAsync(person, cancellationToken);

            var passwordHash = _hasher.Hash("admin123");
            var account = UserAccount.Create(
                personId: person.Id,                 // ⬅️ SetId داخل AddAsync جعلته متاحاً هنا
                username: "admin",
                passwordHash: passwordHash,
                createdAtUtc: _clock.UtcNow,
                createdByUserId: null,                
                mustChangePassword: false);          // TODO: true عندما نبني شاشة تغيير كلمة المرور
            await _users.AddAsync(account, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}