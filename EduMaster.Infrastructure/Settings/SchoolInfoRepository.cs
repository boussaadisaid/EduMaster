using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Settings;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Settings;

/// <summary>هوية المدرسة — جدول صف واحد (Id=1) · الإدراج بمعرّف صريح (لا IDENTITY — بعيداً عن quirk D-122)</summary>
public sealed class SchoolInfoRepository : ISchoolInfoRepository
{
    private readonly IAdoDbSession _session;

    public SchoolInfoRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: بترتيب أعمدة SELECT حرفياً
    private sealed record SchoolInfoRow(
        int Id,
        string Name,
        string? Phone,
        string? Address,
        string? LogoPath,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    public async Task<SchoolInfo?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<SchoolInfoRow>(
            new CommandDefinition(@"
SELECT Id, Name, Phone, Address, LogoPath, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM SchoolInfo
WHERE Id = 1;",
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : SchoolInfo.Load(
            row.Id, row.Name, row.Phone, row.Address, row.LogoPath,
            row.CreatedAtUtc, row.CreatedByUserId, row.UpdatedAtUtc, row.UpdatedByUserId);
    }

    public async Task AddAsync(SchoolInfo info, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الصف الوحيد يُدرج بمعرّف صريح = 1 (القيد CK_SchoolInfo_SingleRow يحرسه)
        const string sql = @"
INSERT INTO SchoolInfo (Id, Name, Phone, Address, LogoPath, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (1, @Name, @Phone, @Address, @LogoPath, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                info.Name,
                info.Phone,
                info.Address,
                info.LogoPath,
                info.CreatedAtUtc,
                info.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        info.SetId(newId);
    }

    public async Task UpdateAsync(SchoolInfo info, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE SchoolInfo
SET Name            = @Name,
    Phone           = @Phone,
    Address         = @Address,
    LogoPath        = @LogoPath,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                info.Name,
                info.Phone,
                info.Address,
                info.LogoPath,
                info.UpdatedAtUtc,
                info.UpdatedByUserId,
                info.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException("SchoolInfo row was not found for update.");
    }
}