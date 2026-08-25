using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Payroll;

/// <summary>
/// مستودع سطور الكشوف (5.2/5.3) — إدراج جماعي صفاً صفاً على نفس المعاملة (العشرات كحد أقصى في كشف شهري) مع تعيين المعرفات فوراً ·
/// حذف المحسوبة لإعادة الحساب (SourceKind=1 — اليدوية تنجو، س-8) · تجميع أعداد السطور · مجاميع المعتمد لكل مستفيد (طرف «المعتمد» من الرصيد الجاري).
/// </summary>
public sealed class PayrollLineRepository : IPayrollLineRepository
{
    private readonly IAdoDbSession _session;

    public PayrollLineRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجل = ترتيب أعمدة الـSELECT حرفياً
    private sealed record LineRow(int Id, int RunId, byte PayeeKind, int? TeacherId, int? EmployeeId, string PayeeName,
        int? PolicyId, byte? Kind, long? RateCentimes, decimal? Percentage, bool? CountsUnjustifiedAbsent,
        decimal Quantity, byte SourceKind, string Details, long AmountCentimes,
        DateTime CreatedAtUtc, int? CreatedByUserId);

    private sealed record CountRow(int RunId, int Cnt);

    private sealed record PayeeTotalRow(byte PayeeKind, int? PayeeId, long Total);

    private const string Columns = "Id, RunId, PayeeKind, TeacherId, EmployeeId, PayeeName, PolicyId, Kind, RateCentimes, Percentage, CountsUnjustifiedAbsent, Quantity, SourceKind, Details, AmountCentimes, CreatedAtUtc, CreatedByUserId";

    private static PayrollLine Map(LineRow row) => PayrollLine.Load(
        row.Id, row.RunId, (PayeeKind)row.PayeeKind, row.TeacherId, row.EmployeeId, row.PayeeName,
        row.PolicyId, (PayPolicyKind?)row.Kind, row.RateCentimes, row.Percentage, row.CountsUnjustifiedAbsent,
        row.Quantity, (LineSourceKind)row.SourceKind, row.Details, row.AmountCentimes, row.CreatedAtUtc, row.CreatedByUserId);

    public async Task<IReadOnlyList<PayrollLine>> GetByRunAsync(int runId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<LineRow>(
            new CommandDefinition($"SELECT {Columns} FROM dbo.PayrollLines WHERE RunId = @RunId ORDER BY Id;",
                new { RunId = runId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task AddRangeAsync(IReadOnlyList<PayrollLine> lines, CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0) return;

        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO dbo.PayrollLines (RunId, PayeeKind, TeacherId, EmployeeId, PayeeName, PolicyId, Kind, RateCentimes, Percentage, CountsUnjustifiedAbsent, Quantity, SourceKind, Details, AmountCentimes, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@RunId, @PayeeKind, @TeacherId, @EmployeeId, @PayeeName, @PolicyId, @Kind, @RateCentimes, @Percentage, @CountsUnjustifiedAbsent, @Quantity, @SourceKind, @Details, @AmountCentimes, @CreatedAtUtc, @CreatedByUserId);";

        foreach (var line in lines)
        {
            var newId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new
                {
                    line.RunId,
                    PayeeKind = (byte)line.PayeeKind,
                    line.TeacherId,
                    line.EmployeeId,
                    line.PayeeName,
                    line.PolicyId,
                    Kind = line.Kind is null ? (byte?)null : (byte)line.Kind.Value,
                    line.RateCentimes,
                    line.Percentage,
                    line.CountsUnjustifiedAbsent,
                    line.Quantity,
                    SourceKind = (byte)line.SourceKind,
                    line.Details,
                    line.AmountCentimes,
                    line.CreatedAtUtc,
                    line.CreatedByUserId
                }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

            line.SetId(newId);
        }
    }

    /// <summary>إعادة الحساب الذرّية (روح D-101): يحذف المحسوبة فقط — اليدوية (SourceKind=2) تنجو.</summary>
    public async Task DeleteComputedForRunAsync(int runId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM dbo.PayrollLines WHERE RunId = @RunId AND SourceKind = 1;",
                new { RunId = runId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    /// <summary>حذف سطر يدوي واحد من مسودة (الحارس في الكيان/الـHandler).</summary>
    public async Task DeleteAsync(int lineId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM dbo.PayrollLines WHERE Id = @Id;",
                new { Id = lineId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<int, int>> GetCountsByRunAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CountRow>(
            new CommandDefinition("SELECT RunId, COUNT(*) AS Cnt FROM dbo.PayrollLines GROUP BY RunId;",
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.RunId, r => r.Cnt);
    }

    /// <summary>مجاميع السطور المعتمدة لكل مستفيد (Σ على كشوف Status=2 فقط — المسودات لا تصنع ديناً) · CASE آمن: قيد OnePayee يضمن أحد المعرّفين.</summary>
    public async Task<IReadOnlyList<PayeeApprovedTotal>> GetApprovedTotalsByPayeeAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PayeeTotalRow>(
            new CommandDefinition(@"
SELECT l.PayeeKind,
       CASE WHEN l.PayeeKind = 1 THEN l.TeacherId ELSE l.EmployeeId END AS PayeeId,
       SUM(l.AmountCentimes) AS Total
FROM dbo.PayrollLines l
INNER JOIN dbo.PayrollRuns r ON r.Id = l.RunId
WHERE r.Status = 2
GROUP BY l.PayeeKind, l.TeacherId, l.EmployeeId;",
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(r => new PayeeApprovedTotal((PayeeKind)r.PayeeKind, r.PayeeId ?? 0, r.Total)).ToList();
    }
}