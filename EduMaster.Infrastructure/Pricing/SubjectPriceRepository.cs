using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Pricing;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Pricing;

public sealed class SubjectPriceRepository : ISubjectPriceRepository
{
    private readonly IAdoDbSession _session;

    public SubjectPriceRepository(IAdoDbSession session) => _session = session;

    private sealed record SubjectPriceRow(
        int Id,
        int AcademicYearId,
        int LevelId,
        int SubjectId,
        long UnitPriceCentimes,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private sealed record SubjectPriceListRow(
        int Id,
        int AcademicYearId,
        string AcademicYearName,
        int LevelId,
        string LevelName,
        int SubjectId,
        string SubjectName,
        long UnitPriceCentimes);

    private const string SelectColumns = @"
SELECT Id, AcademicYearId, LevelId, SubjectId, UnitPriceCentimes, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM SubjectPrices";

    public async Task AddAsync(Domain.Pricing.SubjectPrice subjectPrice, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO SubjectPrices (AcademicYearId, LevelId, SubjectId, UnitPriceCentimes, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@AcademicYearId, @LevelId, @SubjectId, @UnitPriceCentimes, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                subjectPrice.AcademicYearId,
                subjectPrice.LevelId,
                subjectPrice.SubjectId,
                subjectPrice.UnitPriceCentimes,
                subjectPrice.CreatedAtUtc,
                subjectPrice.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        subjectPrice.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Pricing.SubjectPrice subjectPrice, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE SubjectPrices
SET UnitPriceCentimes = @UnitPriceCentimes,
    UpdatedAtUtc      = @UpdatedAtUtc,
    UpdatedByUserId   = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                subjectPrice.UnitPriceCentimes,
                subjectPrice.UpdatedAtUtc,
                subjectPrice.UpdatedByUserId,
                subjectPrice.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"SubjectPrice {subjectPrice.Id} was not found for update.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM SubjectPrices WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"SubjectPrice {id} was not found for delete.");
    }

    public async Task<Domain.Pricing.SubjectPrice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<SubjectPriceRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyExistsAsync(int academicYearId, int levelId, int subjectId, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"SELECT COUNT(*) FROM SubjectPrices
                  WHERE AcademicYearId = @AcademicYearId AND LevelId = @LevelId AND SubjectId = @SubjectId
                    AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { AcademicYearId = academicYearId, LevelId = levelId, SubjectId = subjectId, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IEnumerable<SubjectPriceListItem>> GetByYearAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح (D-40) — أسماء السنة/المستوى/المادة عبر JOIN
        const string sql = @"
SELECT sp.Id, sp.AcademicYearId, ay.Name AS AcademicYearName,
       sp.LevelId, l.Name AS LevelName,
       sp.SubjectId, s.Name AS SubjectName,
       sp.UnitPriceCentimes
FROM SubjectPrices sp
JOIN AcademicYears ay ON ay.Id = sp.AcademicYearId
JOIN Levels l ON l.Id = sp.LevelId
JOIN Subjects s ON s.Id = sp.SubjectId
WHERE (@YearId IS NULL OR sp.AcademicYearId = @YearId)
ORDER BY ay.StartDate DESC, l.SortOrder, s.Name;";

        var rows = await connection.QueryAsync<SubjectPriceListRow>(
            new CommandDefinition(sql, new { YearId = academicYearId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new SubjectPriceListItem(
            row.Id, row.AcademicYearId, row.AcademicYearName,
            row.LevelId, row.LevelName,
            row.SubjectId, row.SubjectName,
            row.UnitPriceCentimes));
    }

    private static Domain.Pricing.SubjectPrice MapToDomain(SubjectPriceRow row) =>
        Domain.Pricing.SubjectPrice.Load(
            id: row.Id,
            academicYearId: row.AcademicYearId,
            levelId: row.LevelId,
            subjectId: row.SubjectId,
            unitPriceCentimes: row.UnitPriceCentimes,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}