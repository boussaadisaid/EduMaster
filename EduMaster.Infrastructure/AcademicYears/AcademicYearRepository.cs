using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Infrastructure.Persistence;



namespace EduMaster.Infrastructure.AcademicYears;

public sealed class AcademicYearRepository : IAcademicYearRepository
{
    private readonly IAdoDbSession _session;

    public AcademicYearRepository(IAdoDbSession session)
    {
        _session = session;
    }

    private sealed record AcademicYearRow(
        int Id,
        string Name,
        DateTime StartDate,
        DateTime EndDate,
        bool IsCurrent,
        bool IsActive,
        long RegistrationFeeCentimes,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    public async Task AddAsync(AcademicYear academicYear, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO AcademicYears
    (Name, StartDate, EndDate, IsCurrent, IsActive, RegistrationFeeCentimes, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES
    (@Name, @StartDate, @EndDate, @IsCurrent, @IsActive, @RegistrationFeeCentimes, @CreatedAtUtc, @CreatedByUserId);";

        // كيان ← معاملات: نفكّك الـVO عند الحدود (Name.Value)
        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                Name = academicYear.Name.Value,
                StartDate = academicYear.StartDate
                    .ToDateTime(TimeOnly.MinValue),

                EndDate = academicYear.EndDate
                    .ToDateTime(TimeOnly.MinValue),
                academicYear.IsCurrent,
                academicYear.IsActive,
                academicYear.RegistrationFeeCentimes,
                academicYear.CreatedAtUtc,
                academicYear.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        academicYear.SetId(newId);
    }

    public async Task UpdateAsync(AcademicYear academicYear, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // قيم التدقيق تُؤخذ من الكيان (ملأها الـHandler عبر IClock/ICurrentUserService — D-20)
        const string sql = @"
UPDATE AcademicYears
SET Name                    = @Name,
    StartDate               = @StartDate,
    EndDate                 = @EndDate,
    IsCurrent               = @IsCurrent,
    IsActive                = @IsActive,
    RegistrationFeeCentimes = @RegistrationFeeCentimes,
    UpdatedAtUtc            = @UpdatedAtUtc,
    UpdatedByUserId         = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                Name = academicYear.Name.Value,
                StartDate = academicYear.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = academicYear.EndDate.ToDateTime(TimeOnly.MinValue),
                academicYear.IsCurrent,
                academicYear.IsActive,
                academicYear.RegistrationFeeCentimes,
                academicYear.UpdatedAtUtc,
                academicYear.UpdatedByUserId,
                academicYear.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"AcademicYear {academicYear.Id} was not found for update.");
    }

    public async Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, Name, StartDate, EndDate, IsCurrent, IsActive, RegistrationFeeCentimes,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM AcademicYears
WHERE Id = @Id;";

        var row = await connection.QuerySingleOrDefaultAsync<AcademicYearRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<AcademicYear?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الفهرس المفلتر UX_AcademicYears_IsCurrent يضمن صفاً واحداً كحد أقصى — QuerySingle آمنة
        const string sql = @"
SELECT Id, Name, StartDate, EndDate, IsCurrent, IsActive, RegistrationFeeCentimes,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM AcademicYears
WHERE IsCurrent = 1;";

        var row = await connection.QuerySingleOrDefaultAsync<AcademicYearRow>(
            new CommandDefinition(sql,
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IEnumerable<AcademicYear>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, Name, StartDate, EndDate, IsCurrent, IsActive, RegistrationFeeCentimes,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM AcademicYears
ORDER BY StartDate DESC;";

        var rows = await connection.QueryAsync<AcademicYearRow>(
            new CommandDefinition(sql,
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToDomain);
    }

    public async Task<bool> AnyWithNameAsync(string name, int excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT COUNT(*) FROM AcademicYears
WHERE Name = @Name AND Id <> @ExcludeId;";

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Name = name, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> AnyOverlappingAsync(DateOnly startDate, DateOnly endDate, int excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // قاعدة التداخل المحسومة: StartDate < النهاية الجديدة AND EndDate > البداية الجديدة
        const string sql = @"
SELECT COUNT(*) FROM AcademicYears
WHERE Id <> @ExcludeId
  AND StartDate < @EndDate
  AND EndDate > @StartDate;";

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                ExcludeId = excludeId,
                StartDate = startDate.ToDateTime(TimeOnly.MinValue),
                EndDate = endDate.ToDateTime(TimeOnly.MinValue)
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        // 2.4: تُفحص الأفواج الفعّالة والتسجيلات هنا — مع بقية حُراس D-55
        return Task.FromResult(false);
    }

    private static AcademicYear MapToDomain(AcademicYearRow row) =>
        AcademicYear.Load(
            id: row.Id,
            name: new YearName(row.Name),
            startDate: DateOnly.FromDateTime(row.StartDate),
            endDate: DateOnly.FromDateTime(row.EndDate),
            isCurrent: row.IsCurrent,
            isActive: row.IsActive,
            registrationFeeCentimes: row.RegistrationFeeCentimes,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}