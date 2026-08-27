using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Billing;

/// <summary>الإيصالات: كتابة + قراءات مسطّحة · اتجاه D-112 مطبَّق: DateOnly تتحوّل DateTime عند معاملات Dapper</summary>
public sealed class PaymentRepository : IPaymentRepository
{
    private readonly IAdoDbSession _session;

    public PaymentRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: بنفس ترتيب استعلام GetForPeriodAsync (4.3) — PaidOn تُقرأ DateTime من DATE
    private sealed record PaymentLogRow(
        int Id,
        int ReceiptNo,
        byte Kind,
        string StudentName,
        string? PayerName,
        long AmountCentimes,
        DateTime PaidOn,
        string? Note,
        long AllocatedCentimes);

    public async Task AddAsync(Domain.Billing.Payment payment, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // ⚠ حدود Dapper (D-112): PaidOn تبقى DateOnly في الدومين (تاريخ عمل نقي)، وتتحوّل إلى DateTime عند المعاملات
        const string sql = @"
INSERT INTO Payments (ReceiptNo, StudentId, PaidByPersonId, Kind, AmountCentimes, PaidOn, Note, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@ReceiptNo, @StudentId, @PaidByPersonId, @Kind, @AmountCentimes, @PaidOn, @Note, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                payment.ReceiptNo,
                payment.StudentId,
                payment.PaidByPersonId,
                Kind = (byte)payment.Kind,
                payment.AmountCentimes,
                PaidOn = payment.PaidOn.ToDateTime(TimeOnly.MinValue),
                payment.Note,
                payment.CreatedAtUtc,
                payment.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        payment.SetId(newId);
    }

    public async Task AddAllocationAsync(Domain.Billing.PaymentAllocation allocation, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // بلا OUTPUT: معرف سطر التخصيص لا يُستهلك — يكفي صفّه في القاعدة
        const string sql = @"
INSERT INTO PaymentAllocations (PaymentId, ChargeId, AmountCentimes, CreatedAtUtc, CreatedByUserId)
VALUES (@PaymentId, @ChargeId, @AmountCentimes, @CreatedAtUtc, @CreatedByUserId);";

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                allocation.PaymentId,
                allocation.ChargeId,
                allocation.AmountCentimes,
                allocation.CreatedAtUtc,
                allocation.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));
    }

    public async Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // يُستدعى داخل معاملة التسجيل — والفهرس الفريد UX_Payments_ReceiptNo يحرس السباق (D-105)
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT ISNULL(MAX(ReceiptNo), 0) + 1 FROM Payments;",
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    public async Task<long> GetUnallocatedForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-107 (مصحَّحة في 4.3): الزائدة الدائنة = Σقبض − Σمخصوص − Σصرف — الصرف ينقصها وإلا صُرِف من الهواء
        const string sql = @"
SELECT ISNULL((SELECT SUM(p.AmountCentimes) FROM Payments p WHERE p.StudentId = @StudentId AND p.Kind = 1), 0)
     - ISNULL((SELECT SUM(a.AmountCentimes) FROM PaymentAllocations a
               JOIN Payments p2 ON p2.Id = a.PaymentId
               WHERE p2.StudentId = @StudentId AND p2.Kind = 1), 0)
     - ISNULL((SELECT SUM(p3.AmountCentimes) FROM Payments p3 WHERE p3.StudentId = @StudentId AND p3.Kind = 2), 0);";

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    // ⚠ D-81: بنفس ترتيب الاستعلام أدناه (6.6 — ز-1)
    private sealed record UnallocatedReceiptRow(int PaymentId, long FreeCentimes);

    public async Task<IReadOnlyList<UnallocatedReceiptRaw>> GetUnallocatedReceiptsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // 6.6 — ز-1: حرّية كل إيصال قبض = مبلغه − Σ تخصيصاته · الحرّة > 0 فقط · الأقدم أولاً (الصرف يُحسم بسقف الإجمالي في المصفف)
        const string sqlFree = @"
SELECT p.Id AS PaymentId,
       p.AmountCentimes - ISNULL((SELECT SUM(a.AmountCentimes) FROM PaymentAllocations a WHERE a.PaymentId = p.Id), 0) AS FreeCentimes
FROM Payments p
WHERE p.StudentId = @StudentId
  AND p.Kind = 1
  AND p.AmountCentimes - ISNULL((SELECT SUM(a.AmountCentimes) FROM PaymentAllocations a WHERE a.PaymentId = p.Id), 0) > 0
ORDER BY p.PaidOn, p.Id;";

        var rows = await connection.QueryAsync<UnallocatedReceiptRow>(
            new CommandDefinition(sqlFree, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(r => new UnallocatedReceiptRaw(r.PaymentId, r.FreeCentimes)).ToList();
    }

    public async Task<IEnumerable<PaymentListItem>> GetForPeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // 4.3: قبض + صرف لفترة — الأحدث برقم الإيصال · D-112: حدودا الفترة تتحوّلان DateTime عند المعاملات
        const string sql = @"
SELECT p.Id, p.ReceiptNo, p.Kind,
       CONCAT_WS(N' ', sp.FirstName, sp.LastName, sp.FatherName) AS StudentName,
       CONCAT_WS(N' ', pp.FirstName, pp.LastName, pp.FatherName) AS PayerName,
       p.AmountCentimes, p.PaidOn, p.Note,
       ISNULL(alloc.SumAllocated, 0) AS AllocatedCentimes
FROM Payments p
JOIN Students s ON s.Id = p.StudentId
JOIN Persons sp ON sp.Id = s.PersonId
LEFT JOIN Persons pp ON pp.Id = p.PaidByPersonId
LEFT JOIN (SELECT PaymentId, SUM(AmountCentimes) AS SumAllocated FROM PaymentAllocations GROUP BY PaymentId) alloc
       ON alloc.PaymentId = p.Id
WHERE p.PaidOn >= @From AND p.PaidOn <= @To
ORDER BY p.ReceiptNo DESC;";

        var rows = await connection.QueryAsync<PaymentLogRow>(
            new CommandDefinition(sql, new
            {
                From = from.ToDateTime(TimeOnly.MinValue),
                To = to.ToDateTime(TimeOnly.MinValue)
            },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new PaymentListItem(
            row.Id, row.ReceiptNo, (PaymentKind)row.Kind, row.StudentName,
            string.IsNullOrWhiteSpace(row.PayerName) ? null : row.PayerName,
            row.AmountCentimes, row.PaidOn, row.Note, row.AllocatedCentimes));
    }

    public async Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM Payments WHERE StudentId = @StudentId;",
                new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }
}