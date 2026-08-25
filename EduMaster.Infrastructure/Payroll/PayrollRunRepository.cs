using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Payroll;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Payroll;

/// <summary>
/// مستودع كشوف الأجور (5.2) — نمط IAdoDbSession/Dapper القائم · أعمدة DATE تُقرأ DateTime وتُحوَّل DateOnly (اتفاق D-112) ·
/// TINYINT ⇄ enum ببايت · الفترة لا تتغير بعد الإنشاء (التحديث للإجمالي وختم الاعتماد فقط).
/// </summary>
public sealed class PayrollRunRepository : IPayrollRunRepository
{
    private readonly IAdoDbSession _session;

    public PayrollRunRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجل = ترتيب أعمدة الـSELECT حرفياً
    private sealed record RunRow(int Id, DateTime PeriodStart, DateTime PeriodEnd, byte Status, long TotalCentimes,
        DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? ApprovedAtUtc, int? ApprovedByUserId);

    private const string Columns = "Id, PeriodStart, PeriodEnd, Status, TotalCentimes, CreatedAtUtc, CreatedByUserId, ApprovedAtUtc, ApprovedByUserId";

    private static PayrollRun Map(RunRow row) => PayrollRun.Load(
        row.Id, DateOnly.FromDateTime(row.PeriodStart), DateOnly.FromDateTime(row.PeriodEnd), (RunStatus)row.Status,
        row.TotalCentimes, row.CreatedAtUtc, row.CreatedByUserId, row.ApprovedAtUtc, row.ApprovedByUserId);

    public async Task<PayrollRun?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<RunRow>(
            new CommandDefinition($"SELECT {Columns} FROM dbo.PayrollRuns WHERE Id = @Id;",
                new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<PayrollRun>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<RunRow>(
            new CommandDefinition($"SELECT {Columns} FROM dbo.PayrollRuns ORDER BY Id;",
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    /// <summary>تداخل المجالين [Start..End] ⟺ Start &lt;= @To AND End &gt;= @From — على المعتمدة فقط (Status=2) · حارس «لا ازدواج احتساب» (روح D-27).</summary>
    public async Task<bool> ExistsApprovedOverlapAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var found = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.PayrollRuns
    WHERE Status = 2 AND PeriodStart <= @To AND PeriodEnd >= @From
) THEN 1 ELSE 0 END;",
                new { From = periodStart.ToDateTime(TimeOnly.MinValue), To = periodEnd.ToDateTime(TimeOnly.MinValue) },
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return found == 1;
    }

    /// <summary>نفس فحص التداخل لكن على المسودات (Status=1) — حارس «لا تكديس مسودات لنفس الفترة».</summary>
    public async Task<bool> ExistsDraftOverlapAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var found = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.PayrollRuns
    WHERE Status = 1 AND PeriodStart <= @To AND PeriodEnd >= @From
) THEN 1 ELSE 0 END;",
                new { From = periodStart.ToDateTime(TimeOnly.MinValue), To = periodEnd.ToDateTime(TimeOnly.MinValue) },
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return found == 1;
    }

    public async Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO dbo.PayrollRuns (PeriodStart, PeriodEnd, Status, TotalCentimes, CreatedAtUtc, CreatedByUserId, ApprovedAtUtc, ApprovedByUserId)
OUTPUT INSERTED.Id
VALUES (@PeriodStart, @PeriodEnd, @Status, @TotalCentimes, @CreatedAtUtc, @CreatedByUserId, @ApprovedAtUtc, @ApprovedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                PeriodStart = run.PeriodStart.ToDateTime(TimeOnly.MinValue),
                PeriodEnd = run.PeriodEnd.ToDateTime(TimeOnly.MinValue),
                Status = (byte)run.Status,
                run.TotalCentimes,
                run.CreatedAtUtc,
                run.CreatedByUserId,
                run.ApprovedAtUtc,
                run.ApprovedByUserId
            }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        run.SetId(newId);
    }

    public async Task UpdateAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(@"
UPDATE dbo.PayrollRuns
SET Status = @Status, TotalCentimes = @TotalCentimes, ApprovedAtUtc = @ApprovedAtUtc, ApprovedByUserId = @ApprovedByUserId
WHERE Id = @Id;",
                new
                {
                    run.Id,
                    Status = (byte)run.Status,
                    run.TotalCentimes,
                    run.ApprovedAtUtc,
                    run.ApprovedByUserId
                }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    /// <summary>حذف مسودة فقط (الحارس في الـHandler) — سطورها تتبعها بـON DELETE CASCADE (016).</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM dbo.PayrollRuns WHERE Id = @Id;",
                new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}