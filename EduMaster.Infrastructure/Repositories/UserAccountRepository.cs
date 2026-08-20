using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Users;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Users;

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly IAdoDbSession _session;

    public UserAccountRepository(IAdoDbSession session)
    {
        _session = session;
    }

    private sealed record UserAccountRow(
        int Id,
        int PersonId,
        string Username,
        string PasswordHash,
        bool IsActive,
        int FailedLoginCount,
        DateTime? LastLoginAtUtc,
        bool MustChangePassword,
        DateTime? LockedUntilUtc,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private const string SelectSql = @"
SELECT Id, PersonId, Username, PasswordHash, IsActive,
       FailedLoginCount, LastLoginAtUtc, MustChangePassword, LockedUntilUtc,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM UserAccounts";

    public async Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserAccountRow>(
            new CommandDefinition(SelectSql + "\nWHERE Username = @Username AND IsDeleted = 0;",
                new { Username = username.Trim() },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<UserAccount?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserAccountRow>(
            new CommandDefinition(SelectSql + "\nWHERE PersonId = @PersonId AND IsDeleted = 0;",
                new { PersonId = personId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM UserAccounts WHERE IsDeleted = 0;",
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> AnyWithUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM UserAccounts WHERE Username = @Username AND IsDeleted = 0;",
                new { Username = username.Trim() },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO UserAccounts
    (PersonId, Username, PasswordHash, IsActive, FailedLoginCount,
     LastLoginAtUtc, MustChangePassword, LockedUntilUtc, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES
    (@PersonId, @Username, @PasswordHash, @IsActive, @FailedLoginCount,
     @LastLoginAtUtc, @MustChangePassword, @LockedUntilUtc, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                account.PersonId,
                account.Username,
                account.PasswordHash,
                account.IsActive,
                account.FailedLoginCount,
                account.LastLoginAtUtc,
                account.MustChangePassword,
                account.LockedUntilUtc,
                account.CreatedAtUtc,
                account.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        account.SetId(newId);
    }

    public async Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-20: قيم التدقيق تُؤخذ من الكيان (السلوكيات تملؤها بساعة ممررة) — لا SYSUTCDATETIME() ولا null ثابت
        const string sql = @"
UPDATE UserAccounts
SET PasswordHash       = @PasswordHash,
    IsActive           = @IsActive,
    FailedLoginCount   = @FailedLoginCount,
    LastLoginAtUtc     = @LastLoginAtUtc,
    MustChangePassword = @MustChangePassword,
    LockedUntilUtc     = @LockedUntilUtc,
    UpdatedAtUtc       = @UpdatedAtUtc,
    UpdatedByUserId    = @UpdatedByUserId
WHERE Id = @Id AND IsDeleted = 0;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                account.PasswordHash,
                account.IsActive,
                account.FailedLoginCount,
                account.LastLoginAtUtc,
                account.MustChangePassword,
                account.LockedUntilUtc,
                account.UpdatedAtUtc,
                account.UpdatedByUserId,
                account.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"UserAccount {account.Id} was not found for update.");
    }

    private static UserAccount MapToDomain(UserAccountRow row) =>
        UserAccount.Load(
            id: row.Id,
            personId: row.PersonId,
            username: row.Username,
            passwordHash: row.PasswordHash,
            isActive: row.IsActive,
            failedLoginCount: row.FailedLoginCount,
            lastLoginAtUtc: row.LastLoginAtUtc,
            mustChangePassword: row.MustChangePassword,
            lockedUntilUtc: row.LockedUntilUtc,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}