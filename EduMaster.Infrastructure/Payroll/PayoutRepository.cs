using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Payroll;

/// <summary>
/// مستودع إيصالات الصرف (5.3) — نمط IAdoDbSession/Dapper القائم · إضافة وقراءة فقط (قداسة الإيصال: لا تعديل ولا حذف — س-5) ·
/// رقم الإيصال MAX+1 داخل معاملة الـHandler (مرآة D-105 — تسلسل موحّد بلا فجوات) والفهرس الفريد يحرسه قاعدةً.
/// </summary>
public sealed class PayoutRepository : IPayoutRepository
{
    private readonly IAdoDbSession _session;

    public PayoutRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجل = ترتيب أعمدة الـSELECT حرفياً
    private sealed record PayoutRow(int Id, int ReceiptNo, byte PayeeKind, int? TeacherId, int? EmployeeId,
        int? PayrollRunId, int TreasuryAccountId, DateTime PayoutDate, long AmountCentimes, string? Note, DateTime CreatedAtUtc, int? CreatedByUserId);

    private sealed record PayeeTotalRow(byte PayeeKind, int? PayeeId, long Total);

    private const string Columns = "Id, ReceiptNo, PayeeKind, TeacherId, EmployeeId, PayrollRunId, TreasuryAccountId, PayoutDate, AmountCentimes, Note, CreatedAtUtc, CreatedByUserId";

    private static Payout Map(PayoutRow row) => Payout.Load(
        row.Id, row.ReceiptNo, (PayeeKind)row.PayeeKind, row.TeacherId, row.EmployeeId, row.PayrollRunId,
        row.TreasuryAccountId, DateOnly.FromDateTime(row.PayoutDate),
        row.AmountCentimes, row.Note, row.CreatedAtUtc, row.CreatedByUserId);

    public async Task AddAsync(Payout payout, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO dbo.Payouts (ReceiptNo, PayeeKind, TeacherId, EmployeeId, PayrollRunId, TreasuryAccountId, PayoutDate, AmountCentimes, Note, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@ReceiptNo, @PayeeKind, @TeacherId, @EmployeeId, @PayrollRunId, @TreasuryAccountId, @PayoutDate, @AmountCentimes, @Note, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                payout.ReceiptNo,
                PayeeKind = (byte)payout.PayeeKind,
                payout.TeacherId,
                payout.EmployeeId,
                payout.PayrollRunId,
                payout.TreasuryAccountId,
                PayoutDate = payout.PayoutDate.ToDateTime(TimeOnly.MinValue),
                payout.AmountCentimes,
                payout.Note,
                payout.CreatedAtUtc,
                payout.CreatedByUserId
            }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        payout.SetId(newId);
    }

    /// <summary>التسلسل التالي بلا فجوات — داخل معاملة الـHandler (الفريد UQ_Payouts_ReceiptNo يحرس السباق نظرياً — مرآة D-105).</summary>
    public async Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT ISNULL(MAX(ReceiptNo), 0) + 1 FROM dbo.Payouts;",
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    /// <summary>مجاميع المصروف لكل مستفيد عبر التاريخ — طرف «المصروف» من الرصيد الجاري · CASE آمن: قيد OnePayee يضمن أحد المعرّفين.</summary>
    public async Task<IReadOnlyList<PayeePayoutTotal>> GetTotalsByPayeeAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PayeeTotalRow>(
            new CommandDefinition(@"
SELECT PayeeKind,
       CASE WHEN PayeeKind = 1 THEN TeacherId ELSE EmployeeId END AS PayeeId,
       SUM(AmountCentimes) AS Total
FROM dbo.Payouts
GROUP BY PayeeKind, TeacherId, EmployeeId;",
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(r => new PayeePayoutTotal((PayeeKind)r.PayeeKind, r.PayeeId ?? 0, r.Total)).ToList();
    }

    /// <summary>إيصالات مستفيد واحد — الأحدث برقم إيصال أكبر أولاً (لديالوغ الصرف).</summary>
    public async Task<IReadOnlyList<Payout>> GetForPayeeAsync(PayeeKind payeeKind, int payeeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var sql = payeeKind == PayeeKind.Teacher
            ? $"SELECT {Columns} FROM dbo.Payouts WHERE PayeeKind = 1 AND TeacherId = @PayeeId ORDER BY ReceiptNo DESC;"
            : $"SELECT {Columns} FROM dbo.Payouts WHERE PayeeKind = 2 AND EmployeeId = @PayeeId ORDER BY ReceiptNo DESC;";

        var rows = await connection.QueryAsync<PayoutRow>(
            new CommandDefinition(sql, new { PayeeId = payeeId },
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }
}