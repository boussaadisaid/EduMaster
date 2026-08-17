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
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);


    public async Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT Id, PersonId, Username, PasswordHash, IsActive,
                   FailedLoginCount, LastLoginAtUtc, MustChangePassword,
                   CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
            FROM UserAccounts
            WHERE Username = @Username AND IsDeleted = 0;";

        // ② new { Username = ... } — Dapper يحوّلها لمعاملات آمنة: نفس حماية SQL Injection
        var row = await connection.QuerySingleOrDefaultAsync<UserAccountRow>(
            new CommandDefinition(sql, new { Username = username.Trim() },
                transaction: _session.CurrentTransaction,      // ③ نمرّر المعاملة دائماً
                cancellationToken: cancellationToken));

        // ④ الصف الخام يدخل مصنع Load — الدومين يبقى سيد قواعده
        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = "SELECT COUNT(*) FROM UserAccounts WHERE IsDeleted = 0;";

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql,
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
                 LastLoginAtUtc, MustChangePassword, CreatedAtUtc, CreatedByUserId)
            OUTPUT INSERTED.Id
            VALUES
                (@PersonId, @Username, @PasswordHash, @IsActive, @FailedLoginCount,
                 @LastLoginAtUtc, @MustChangePassword, @CreatedAtUtc, @CreatedByUserId);";

        // نفس درس OUTPUT INSERTED — يستلم قيمة IDENTITY في نفس الرحلة
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

        const string sql = @"
            UPDATE UserAccounts
            SET PasswordHash       = @PasswordHash,
                IsActive           = @IsActive,
                FailedLoginCount   = @FailedLoginCount,
                LastLoginAtUtc     = @LastLoginAtUtc,
                MustChangePassword = @MustChangePassword,
                UpdatedAtUtc       = SYSUTCDATETIME(),
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
                UpdatedByUserId = (int?)null,   // في تدفق الدخول: لا مستخدم داخل بعد
                Id = account.Id
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
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);




}