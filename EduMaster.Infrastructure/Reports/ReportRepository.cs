using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Reports;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Reports;

/// <summary>قراءات التقارير (F6) — خاماً حرفياً (D-128) · سجلات الصفوف بترتيب أعمدة SELECT (D-81)</summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly IAdoDbSession _session;

    public ReportRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: بترتيب أعمدة SELECT حرفياً
    private sealed record PaymentRawRow(int Id, int ReceiptNo, byte Kind, string? PayerName,
        long AmountCentimes, DateTime PaidOn, string? Note, long AllocatedCentimes);

    private sealed record AllocationRawRow(int PaymentId, int ChargeId, long AmountCentimes);

    // 6.3 — سطر الإيصال المفرد (بترتيب SELECT حرفياً)
    private sealed record ReceiptPrintRow(int Id, int ReceiptNo, byte Kind, int StudentId, string StudentName,
        string? PayerName, long AmountCentimes, DateTime PaidOn, string? Note);

    private sealed record ReceiptAllocationRow(int ChargeId, long AmountCentimes);

    // 6.4 — سجلات خام (بترتيب SELECT حرفياً)
    private sealed record AttendanceMarkRow(int StudentId, string StudentName, int ClassGroupId, string GroupName, byte Status);

    private sealed record EnrollmentBalanceRow(int EnrollmentId, int StudentId, string StudentName,
        int ClassGroupId, string GroupName, string SubjectName,
        int PurchasedSessions, int ConsumedSessions,
        string? GuardianName, string? GuardianPhone, string? StudentPhone);

    public async Task<StudentPaymentsRead> GetPaymentsWithAllocationsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var paymentRows = await connection.QueryAsync<PaymentRawRow>(
            new CommandDefinition(@"
SELECT p.Id, p.ReceiptNo, p.Kind, CONCAT_WS(N' ', pp.FirstName, pp.LastName, pp.FatherName) AS PayerName,
       p.AmountCentimes, p.PaidOn, p.Note,
       ISNULL((SELECT SUM(a.AmountCentimes) FROM PaymentAllocations a WHERE a.PaymentId = p.Id), 0) AS AllocatedCentimes
FROM Payments p
LEFT JOIN Persons pp ON pp.Id = p.PaidByPersonId
WHERE p.StudentId = @StudentId
ORDER BY p.PaidOn, p.Id;",
                new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        var allocationRows = await connection.QueryAsync<AllocationRawRow>(
            new CommandDefinition(@"
SELECT a.PaymentId, a.ChargeId, a.AmountCentimes
FROM PaymentAllocations a
JOIN Payments p ON p.Id = a.PaymentId
WHERE p.StudentId = @StudentId
ORDER BY a.PaymentId, a.Id;",
                new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        var payments = paymentRows.Select(r => new StudentPaymentRaw(
            r.Id, r.ReceiptNo, (PaymentKind)r.Kind, r.PayerName,
            r.AmountCentimes, r.PaidOn, r.Note, r.AllocatedCentimes)).ToList();

        var allocations = allocationRows.Select(r => new StudentPaymentAllocationRaw(
            r.PaymentId, r.ChargeId, r.AmountCentimes)).ToList();

        return new StudentPaymentsRead(payments, allocations);
    }

    public async Task<ReceiptPrintRead?> GetReceiptForPrintAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ReceiptPrintRow>(
            new CommandDefinition(@"
SELECT p.Id, p.ReceiptNo, p.Kind, p.StudentId,
       CONCAT_WS(N' ', sp.FirstName, sp.LastName, sp.FatherName) AS StudentName,
       CONCAT_WS(N' ', pp.FirstName, pp.LastName, pp.FatherName) AS PayerName,
       p.AmountCentimes, p.PaidOn, p.Note
FROM Payments p
JOIN Students s ON s.Id = p.StudentId
JOIN Persons sp ON sp.Id = s.PersonId
LEFT JOIN Persons pp ON pp.Id = p.PaidByPersonId
WHERE p.Id = @PaymentId;",
                new { PaymentId = paymentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (row is null) return null;

        var allocationRows = await connection.QueryAsync<ReceiptAllocationRow>(
            new CommandDefinition(@"
SELECT a.ChargeId, a.AmountCentimes
FROM PaymentAllocations a
WHERE a.PaymentId = @PaymentId
ORDER BY a.Id;",
                new { PaymentId = paymentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        var allocations = allocationRows.Select(r => new ReceiptAllocationLineRaw(r.ChargeId, r.AmountCentimes)).ToList();

        return new ReceiptPrintRead(
            row.Id, row.ReceiptNo, (PaymentKind)row.Kind,
            row.StudentId, row.StudentName, row.PayerName,
            row.AmountCentimes, row.PaidOn, row.Note,
            allocations);
    }

    /// <summary>ق-1 (6.4): علامات الحصص المُقامة في [from, toExclusive) بتوقيت العمل المحلي — خاماً، والتجميع في الـHandler (D-128)</summary>
    public async Task<IReadOnlyList<AttendanceMarkRaw>> GetAttendanceMarksForPeriodAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<AttendanceMarkRow>(
            new CommandDefinition(@"
SELECT st.Id AS StudentId,
       CONCAT_WS(N' ', sp.FirstName, sp.LastName, sp.FatherName) AS StudentName,
       s.ClassGroupId,
       g.Name AS GroupName,
       a.Status
FROM SessionAttendance a
JOIN ClassSessions s ON s.Id = a.ClassSessionId
JOIN ClassGroupEnrollments e ON e.Id = a.ClassGroupEnrollmentId
JOIN Students st ON st.Id = e.StudentId
JOIN Persons sp ON sp.Id = st.PersonId
JOIN ClassGroups g ON g.Id = s.ClassGroupId
WHERE s.Status = 2   -- المُقامة فقط (D-100: الحضور على المُقامة)
  AND s.StartsAt >= @From AND s.StartsAt < @ToExclusive
  AND (@ClassGroupId IS NULL OR s.ClassGroupId = @ClassGroupId)
ORDER BY sp.FirstName, sp.LastName, g.Name, s.StartsAt;",
                new { From = from, ToExclusive = toExclusive, ClassGroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(r => new AttendanceMarkRaw(
            r.StudentId, r.StudentName, r.ClassGroupId, r.GroupName, (AttendanceStatus)r.Status)).ToList();
    }

    /// <summary>ق-5 (6.4): أرصدة التسجيلات النشطة في أفواج فعّالة — تعبيرا المشتريات/المخصوم مأخوذان حرفاً من قراءة «أفواجه» القائمة (D-81: لا صيغة ثانية للحقيقة) · الفلترة والترتيب في الـHandler</summary>
    public async Task<IReadOnlyList<EnrollmentBalanceRaw>> GetActiveEnrollmentBalancesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<EnrollmentBalanceRow>(
            new CommandDefinition(@"
SELECT e.Id AS EnrollmentId, e.StudentId,
       CONCAT_WS(N' ', sp.FirstName, sp.LastName, sp.FatherName) AS StudentName,
       e.ClassGroupId, g.Name AS GroupName, sb.Name AS SubjectName,
       (SELECT ISNULL(SUM(p.SessionsCount), 0) FROM GroupSessionPurchases p WHERE p.ClassGroupEnrollmentId = e.Id) AS PurchasedSessions,
       (SELECT COUNT(*) FROM SessionAttendance sa WHERE sa.ClassGroupEnrollmentId = e.Id AND sa.Status IN (1, 2)) AS ConsumedSessions,
       CONCAT_WS(N' ', gp.FirstName, gp.LastName, gp.FatherName) AS GuardianName,
       gp.Phone AS GuardianPhone,
       sp.Phone AS StudentPhone
FROM ClassGroupEnrollments e
JOIN ClassGroups g ON g.Id = e.ClassGroupId
JOIN AcademicYears ay ON ay.Id = g.AcademicYearId
JOIN Subjects sb ON sb.Id = g.SubjectId
JOIN Students st ON st.Id = e.StudentId
JOIN Persons sp ON sp.Id = st.PersonId
LEFT JOIN Persons gp ON gp.Id = st.GuardianPersonId
WHERE e.Status = 1 AND g.IsActive = 1 AND ay.IsCurrent = 1
ORDER BY sp.FirstName, sp.LastName;",
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(r => new EnrollmentBalanceRaw(
            r.EnrollmentId, r.StudentId, r.StudentName,
            r.ClassGroupId, r.GroupName, r.SubjectName,
            r.PurchasedSessions, r.ConsumedSessions,
            r.GuardianName, r.GuardianPhone, r.StudentPhone)).ToList();
    }
}
