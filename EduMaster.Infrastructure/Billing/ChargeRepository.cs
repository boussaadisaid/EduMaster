using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Billing;

public sealed class ChargeRepository : IChargeRepository
{
    private readonly IAdoDbSession _session;

    public ChargeRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجل = ترتيب أعمدة الـSELECT حرفياً
    private sealed record ChargeRow(
        int Id,
        int StudentId,
        byte Kind,
        int? AnnualEnrollmentId,
        int? GroupSessionPurchaseId,
        long OriginalAmountCentimes,
        long AmountCentimes,
        byte Status,
        string? AdjustmentNote,
        DateTime? CancelledAtUtc,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    // ⚠ D-81: بنفس ترتيب استعلام GetForStudentAsync — AllocatedCentimes في الذيل (إضافة 4.2)
    private sealed record StudentChargeRow(
        int Id,
        int StudentId,
        byte Kind,
        string SourceDescription,
        long OriginalAmountCentimes,
        long AmountCentimes,
        byte Status,
        string? AdjustmentNote,
        DateTime CreatedAtUtc,
        long AllocatedCentimes,
        int? AcademicYearId,
        string? AcademicYearName);

    // ⚠ D-81: بنفس ترتيب استعلام GetOpenForStudentAsync
    private sealed record OpenChargeRow(
        int Id,
        byte Kind,
        string SourceDescription,
        long AmountCentimes,
        long AllocatedCentimes,
        DateTime CreatedAtUtc,
        int? AcademicYearId,
        string? AcademicYearName);

    // ⚠ D-81: بنفس ترتيب استعلام GetDebtorsAsync (4.3)
    private sealed record DebtorRow(
        int StudentId,
        string FullName,
        string? Phone,
        int OpenChargesCount,
        long RemainingCentimes);

    private const string SelectColumns = @"
SELECT Id, StudentId, Kind, AnnualEnrollmentId, GroupSessionPurchaseId, OriginalAmountCentimes, AmountCentimes,
       Status, AdjustmentNote, CancelledAtUtc, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM Charges";

    // وصف المصدر العربي المشترك بين القراءتين (D-63: بلا ToString)
    private const string SourceDescriptionSql = @"
CASE WHEN c.Kind = 1 THEN N'حقوق تسجيل — ' + ISNULL(ay.Name, N'')
     ELSE N'حزمة ' + CAST(ISNULL(p.SessionsCount, 0) AS nvarchar(10)) + N' حصص — ' + ISNULL(cg.Name, N'') END";

    private const string SourceJoins = @"
LEFT JOIN AnnualEnrollments ae ON ae.Id = c.AnnualEnrollmentId
LEFT JOIN AcademicYears ay ON ay.Id = ae.AcademicYearId
LEFT JOIN GroupSessionPurchases p ON p.Id = c.GroupSessionPurchaseId
LEFT JOIN ClassGroupEnrollments cge ON cge.Id = p.ClassGroupEnrollmentId
LEFT JOIN ClassGroups cg ON cg.Id = cge.ClassGroupId
LEFT JOIN AcademicYears gay ON gay.Id = cg.AcademicYearId";

    public async Task AddAsync(Domain.Billing.Charge charge, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO Charges (StudentId, Kind, AnnualEnrollmentId, GroupSessionPurchaseId, OriginalAmountCentimes, AmountCentimes,
                     Status, AdjustmentNote, CancelledAtUtc, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@StudentId, @Kind, @AnnualEnrollmentId, @GroupSessionPurchaseId, @OriginalAmountCentimes, @AmountCentimes,
        @Status, @AdjustmentNote, @CancelledAtUtc, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                charge.StudentId,
                Kind = (byte)charge.Kind,
                charge.AnnualEnrollmentId,
                charge.GroupSessionPurchaseId,
                charge.OriginalAmountCentimes,
                charge.AmountCentimes,
                Status = (byte)charge.Status,
                charge.AdjustmentNote,
                charge.CancelledAtUtc,
                charge.CreatedAtUtc,
                charge.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        charge.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Billing.Charge charge, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // التسوية فقط (D-108): الحالة/المبلغ الحالي/السبب/الإلغاء/التدقيق — النوع والمصدر والأصلي ثوابت
        const string sql = @"
UPDATE Charges
SET Status          = @Status,
    AmountCentimes  = @AmountCentimes,
    AdjustmentNote  = @AdjustmentNote,
    CancelledAtUtc  = @CancelledAtUtc,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                Status = (byte)charge.Status,
                charge.AmountCentimes,
                charge.AdjustmentNote,
                charge.CancelledAtUtc,
                charge.UpdatedAtUtc,
                charge.UpdatedByUserId,
                charge.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Charge {charge.Id} was not found for update.");
    }

    public async Task<Domain.Billing.Charge?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ChargeRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IEnumerable<StudentChargeItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الأحدث أولاً · المخصوص في الذيل (D-81)
        var sql = $@"
SELECT c.Id, c.StudentId, c.Kind,
       {SourceDescriptionSql} AS SourceDescription,
       c.OriginalAmountCentimes, c.AmountCentimes, c.Status, c.AdjustmentNote, c.CreatedAtUtc,
       (SELECT ISNULL(SUM(a.AmountCentimes), 0) FROM PaymentAllocations a WHERE a.ChargeId = c.Id) AS AllocatedCentimes,
       CASE WHEN c.Kind = 1 THEN ay.Id ELSE gay.Id END AS AcademicYearId,
       CASE WHEN c.Kind = 1 THEN ay.Name ELSE gay.Name END AS AcademicYearName
FROM Charges c
{SourceJoins}
WHERE c.StudentId = @StudentId
ORDER BY c.CreatedAtUtc DESC;";

        var rows = await connection.QueryAsync<StudentChargeRow>(
            new CommandDefinition(sql, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new StudentChargeItem(
            row.Id, row.StudentId, (ChargeKind)row.Kind, row.SourceDescription,
            row.OriginalAmountCentimes, row.AmountCentimes, (ChargeStatus)row.Status,
            row.AdjustmentNote, row.CreatedAtUtc, row.AllocatedCentimes,
            row.AcademicYearId, row.AcademicYearName));
    }

    public async Task<long> GetAllocatedForChargeAsync(int chargeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // 6.6-ع-3: مجموع تخصيصات مستحق — لحارس «لا تخفيض تحت المخصوص» (ولعكس الإلغاء في ع-ب)
        const string sql = @"
SELECT ISNULL(SUM(a.AmountCentimes), 0)
FROM PaymentAllocations a
WHERE a.ChargeId = @ChargeId;";

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, new { ChargeId = chargeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<OpenChargeItem>> GetOpenForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-106: فعّالة بمتبقٍّ > 0 — الأقدم أولاً للاقتراح التلقائي
        var sql = $@"
SELECT c.Id, c.Kind,
       {SourceDescriptionSql} AS SourceDescription,
       c.AmountCentimes,
       (SELECT ISNULL(SUM(a.AmountCentimes), 0) FROM PaymentAllocations a WHERE a.ChargeId = c.Id) AS AllocatedCentimes,
       c.CreatedAtUtc,
       CASE WHEN c.Kind = 1 THEN ay.Id ELSE gay.Id END AS AcademicYearId,
       CASE WHEN c.Kind = 1 THEN ay.Name ELSE gay.Name END AS AcademicYearName
FROM Charges c
{SourceJoins}
WHERE c.StudentId = @StudentId
  AND c.Status = 1
  AND c.AmountCentimes > (SELECT ISNULL(SUM(a.AmountCentimes), 0) FROM PaymentAllocations a WHERE a.ChargeId = c.Id)
ORDER BY c.CreatedAtUtc;";

        var rows = await connection.QueryAsync<OpenChargeRow>(
            new CommandDefinition(sql, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new OpenChargeItem(
            row.Id, (ChargeKind)row.Kind, row.SourceDescription,
            row.AmountCentimes, row.AllocatedCentimes, row.CreatedAtUtc,
            row.AcademicYearId, row.AcademicYearName));
    }

    public async Task<IEnumerable<DebtorItem>> GetDebtorsAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // 4.3: من عليهم متبقٍّ > 0 — الأكبر أولاً · بحث مبسّط بلا تطبيع (يُعمَّم مع الصيانة/F6)
        const string sql = @"
SELECT s.Id AS StudentId,
       CONCAT_WS(N' ', p.FirstName, p.LastName, p.FatherName) AS FullName,
       p.Phone,
       COUNT(*) AS OpenChargesCount,
       SUM(c.AmountCentimes - ISNULL(alloc.SumAllocated, 0)) AS RemainingCentimes
FROM Charges c
JOIN Students s ON s.Id = c.StudentId AND s.IsDeleted = 0
JOIN Persons p ON p.Id = s.PersonId AND p.IsDeleted = 0
LEFT JOIN AnnualEnrollments ae ON ae.Id = c.AnnualEnrollmentId
LEFT JOIN AcademicYears ay ON ay.Id = ae.AcademicYearId
LEFT JOIN GroupSessionPurchases pch ON pch.Id = c.GroupSessionPurchaseId
LEFT JOIN ClassGroupEnrollments cge ON cge.Id = pch.ClassGroupEnrollmentId
LEFT JOIN ClassGroups cg ON cg.Id = cge.ClassGroupId
LEFT JOIN AcademicYears gay ON gay.Id = cg.AcademicYearId
LEFT JOIN (SELECT ChargeId, SUM(AmountCentimes) AS SumAllocated FROM PaymentAllocations GROUP BY ChargeId) alloc
       ON alloc.ChargeId = c.Id
WHERE c.Status = 1
  AND (ay.IsCurrent = 1 OR gay.IsCurrent = 1)
  AND (@Pattern IS NULL OR p.FirstName LIKE @Pattern OR p.LastName LIKE @Pattern
       OR p.FatherName LIKE @Pattern OR p.Phone LIKE @Pattern)
GROUP BY s.Id, p.FirstName, p.LastName, p.FatherName, p.Phone
HAVING SUM(c.AmountCentimes - ISNULL(alloc.SumAllocated, 0)) > 0
ORDER BY RemainingCentimes DESC;";

        var pattern = string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm.Trim()}%";

        var rows = await connection.QueryAsync<DebtorRow>(
            new CommandDefinition(sql, new { Pattern = pattern },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new DebtorItem(
            row.StudentId, row.FullName, row.Phone, row.OpenChargesCount, row.RemainingCentimes));
    }

    public async Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM Charges WHERE StudentId = @StudentId;",
                new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    private static Domain.Billing.Charge MapToDomain(ChargeRow row) =>
        Domain.Billing.Charge.Load(
            id: row.Id,
            studentId: row.StudentId,
            kind: (ChargeKind)row.Kind,
            annualEnrollmentId: row.AnnualEnrollmentId,
            groupSessionPurchaseId: row.GroupSessionPurchaseId,
            originalAmountCentimes: row.OriginalAmountCentimes,
            amountCentimes: row.AmountCentimes,
            status: (ChargeStatus)row.Status,
            adjustmentNote: row.AdjustmentNote,
            cancelledAtUtc: row.CancelledAtUtc,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}