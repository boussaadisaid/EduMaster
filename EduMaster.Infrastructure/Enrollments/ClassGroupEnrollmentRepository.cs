using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Enrollments;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Enrollments;

public sealed class ClassGroupEnrollmentRepository : IClassGroupEnrollmentRepository
{
    private readonly IAdoDbSession _session;

    public ClassGroupEnrollmentRepository(IAdoDbSession session) => _session = session;

    private sealed record ClassGroupEnrollmentRow(
        int Id,
        int ClassGroupId,
        int StudentId,
        int AnnualEnrollmentId,
        byte Status,
        long SnapshotUnitPriceCentimes,
        long AgreedUnitPriceCentimes,
        string? DiscountNote,
        DateTime EnrolledAtUtc,
        DateTime? WithdrawnAtUtc,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private sealed record RosterRow(
        int Id,
        int StudentId,
        string FirstName,
        string LastName,
        string? FatherName,
        string? Phone,
        byte Status,
        long SnapshotUnitPriceCentimes,
        long AgreedUnitPriceCentimes,
        string? DiscountNote,
        DateTime EnrolledAtUtc,
        DateTime? WithdrawnAtUtc);

    // ⚠ D-81: ترتيب السجل = ترتيب أعمدة الـSELECT حرفياً (عمودا الرصيد في الذيل)
    private sealed record StudentGroupRow(
        int Id,
        int ClassGroupId,
        string ClassGroupName,
        string SubjectName,
        string AcademicYearName,
        byte Status,
        long AgreedUnitPriceCentimes,
        DateTime EnrolledAtUtc,
        int PurchasedSessions,
        int TransferredInSessions,
        int TransferredOutSessions,
        int ConsumedSessions);

    // صف الفوج المسطّح الموحّد (أهداف النقل + المؤهَّلة) — ⚠ D-81: الترتيب = ترتيب أعمدة الـSELECT حرفياً
    private sealed record EligibleGroupRow(
        int Id,
        int AcademicYearId,
        string AcademicYearName,
        int LevelId,
        string LevelName,
        int SubjectId,
        string SubjectName,
        int? TeacherId,
        string? TeacherFirstName,
        string? TeacherLastName,
        string? TeacherFatherName,
        int? RoomId,
        string? RoomName,
        string Name,
        int? Capacity,
        bool IsActive,
        string? StreamsText,
        int EnrolledCount);

    private sealed record TransferContextRow(
        int StudentId,
        int AcademicYearId,
        int ClassGroupId,
        int LevelId,
        int? StreamId);

    private const string SelectColumns = @"
SELECT Id, ClassGroupId, StudentId, AnnualEnrollmentId, Status, SnapshotUnitPriceCentimes, AgreedUnitPriceCentimes,
       DiscountNote, EnrolledAtUtc, WithdrawnAtUtc, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM ClassGroupEnrollments";

    // قائمة أعمدة الفوج المسطّحة المشتركة (D-81: نفس ترتيب EligibleGroupRow حرفاً بحرف)
    private const string EligibleGroupSelect = @"
SELECT cg.Id, cg.AcademicYearId, ay.Name AS AcademicYearName,
       cg.LevelId, l.Name AS LevelName,
       cg.SubjectId, sb.Name AS SubjectName,
       cg.TeacherId, tp.FirstName AS TeacherFirstName, tp.LastName AS TeacherLastName, tp.FatherName AS TeacherFatherName,
       cg.RoomId, r.Name AS RoomName,
       cg.Name, cg.Capacity, cg.IsActive,
       (SELECT STRING_AGG(s.Name, N'، ')
        FROM ClassGroupStreams cgs
        JOIN Streams s ON s.Id = cgs.StreamId
        WHERE cgs.ClassGroupId = cg.Id) AS StreamsText,
       (SELECT COUNT(*) FROM ClassGroupEnrollments e WHERE e.ClassGroupId = cg.Id AND e.Status = 1) AS EnrolledCount
FROM ClassGroups cg
JOIN AcademicYears ay ON ay.Id = cg.AcademicYearId
JOIN Levels l ON l.Id = cg.LevelId
JOIN Subjects sb ON sb.Id = cg.SubjectId
LEFT JOIN Teachers t ON t.Id = cg.TeacherId AND t.IsDeleted = 0
LEFT JOIN Persons tp ON tp.Id = t.PersonId AND tp.IsDeleted = 0
LEFT JOIN Rooms r ON r.Id = cg.RoomId";

    public async Task AddAsync(Domain.Enrollments.ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO ClassGroupEnrollments
    (ClassGroupId, StudentId, AnnualEnrollmentId, Status, SnapshotUnitPriceCentimes, AgreedUnitPriceCentimes,
     DiscountNote, EnrolledAtUtc, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES
    (@ClassGroupId, @StudentId, @AnnualEnrollmentId, @Status, @SnapshotUnitPriceCentimes, @AgreedUnitPriceCentimes,
     @DiscountNote, @EnrolledAtUtc, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                enrollment.ClassGroupId,
                enrollment.StudentId,
                enrollment.AnnualEnrollmentId,
                Status = (byte)enrollment.Status,
                enrollment.SnapshotUnitPriceCentimes,
                enrollment.AgreedUnitPriceCentimes,
                enrollment.DiscountNote,
                enrollment.EnrolledAtUtc,
                enrollment.CreatedAtUtc,
                enrollment.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        enrollment.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Enrollments.ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الهوية والأسعار ثوابت بعد الإلحاق في 2.4 — التحديث للحالة فقط (انسحاب/نقل عبر صفوف)
        const string sql = @"
UPDATE ClassGroupEnrollments
SET Status          = @Status,
    WithdrawnAtUtc  = @WithdrawnAtUtc,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                Status = (byte)enrollment.Status,
                enrollment.WithdrawnAtUtc,
                enrollment.UpdatedAtUtc,
                enrollment.UpdatedByUserId,
                enrollment.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"ClassGroupEnrollment {enrollment.Id} was not found for update.");
    }

    public async Task<Domain.Enrollments.ClassGroupEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClassGroupEnrollmentRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyActiveForStudentInGroupAsync(int classGroupId, int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM ClassGroupEnrollments WHERE ClassGroupId = @ClassGroupId AND StudentId = @StudentId AND Status = 1;",
                new { ClassGroupId = classGroupId, StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<int> CountActiveInGroupAsync(int classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM ClassGroupEnrollments WHERE ClassGroupId = @ClassGroupId AND Status = 1;",
                new { ClassGroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<ClassGroupEnrollmentListItem>> GetForGroupAsync(int classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // قائمة الفوج مسطّحة (D-40) — النشطون أولاً
        const string sql = @"
SELECT cge.Id, cge.StudentId, p.FirstName, p.LastName, p.FatherName, p.Phone,
       cge.Status, cge.SnapshotUnitPriceCentimes, cge.AgreedUnitPriceCentimes, cge.DiscountNote,
       cge.EnrolledAtUtc, cge.WithdrawnAtUtc
FROM ClassGroupEnrollments cge
JOIN Students s ON s.Id = cge.StudentId AND s.IsDeleted = 0
JOIN Persons p ON p.Id = s.PersonId AND p.IsDeleted = 0
WHERE cge.ClassGroupId = @ClassGroupId
ORDER BY cge.Status, p.FirstName, p.LastName;";

        var rows = await connection.QueryAsync<RosterRow>(
            new CommandDefinition(sql, new { ClassGroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new ClassGroupEnrollmentListItem(
            row.Id, row.StudentId, row.FirstName, row.LastName, row.FatherName, row.Phone,
            (EnrollmentStatus)row.Status,
            row.SnapshotUnitPriceCentimes, row.AgreedUnitPriceCentimes, row.DiscountNote,
            row.EnrolledAtUtc, row.WithdrawnAtUtc));
    }

    public async Task<IEnumerable<StudentGroupEnrollmentItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT cge.Id, cge.ClassGroupId, cg.Name AS ClassGroupName, sb.Name AS SubjectName, ay.Name AS AcademicYearName,
       cge.Status, cge.AgreedUnitPriceCentimes, cge.EnrolledAtUtc,
       (SELECT ISNULL(SUM(p.SessionsCount), 0) FROM GroupSessionPurchases p WHERE p.ClassGroupEnrollmentId = cge.Id) AS PurchasedSessions,
       (SELECT ISNULL(SUM(t.SessionsCount), 0) FROM GroupSessionTransfers t WHERE t.ToClassGroupEnrollmentId = cge.Id) AS TransferredInSessions,
       (SELECT ISNULL(SUM(t.SessionsCount), 0) FROM GroupSessionTransfers t WHERE t.FromClassGroupEnrollmentId = cge.Id) AS TransferredOutSessions,
       (SELECT COUNT(*) FROM SessionAttendance sa WHERE sa.ClassGroupEnrollmentId = cge.Id AND sa.Status IN (1, 2)) AS ConsumedSessions
FROM ClassGroupEnrollments cge
JOIN ClassGroups cg ON cg.Id = cge.ClassGroupId
JOIN Subjects sb ON sb.Id = cg.SubjectId
JOIN AcademicYears ay ON ay.Id = cg.AcademicYearId
WHERE cge.StudentId = @StudentId
ORDER BY ay.StartDate DESC, cge.EnrolledAtUtc DESC;";

        var rows = await connection.QueryAsync<StudentGroupRow>(
            new CommandDefinition(sql, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new StudentGroupEnrollmentItem(
            row.Id, row.ClassGroupId, row.ClassGroupName, row.SubjectName, row.AcademicYearName,
            (EnrollmentStatus)row.Status, row.AgreedUnitPriceCentimes, row.EnrolledAtUtc,
            row.PurchasedSessions, row.ConsumedSessions)
        {
            TransferredInSessions = row.TransferredInSessions,
            TransferredOutSessions = row.TransferredOutSessions
        });
    }

    public async Task<IEnumerable<StudentGroupEnrollmentItem>> GetForStudentAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // «أفواجه» مسطّحة (D-40) — الأحدث أولاً · عمودا الرصيد في الذيل (D-81)
        // D-93: المخصوم = عدد علامات الحاضر والغائب — المبرر (3) لا يخصم · تاريخ المنسحب يبقى محسوباً (D-102)
        const string sql = @"
SELECT cge.Id, cge.ClassGroupId, cg.Name AS ClassGroupName, sb.Name AS SubjectName, ay.Name AS AcademicYearName,
       cge.Status, cge.AgreedUnitPriceCentimes, cge.EnrolledAtUtc,
       (SELECT ISNULL(SUM(p.SessionsCount), 0) FROM GroupSessionPurchases p WHERE p.ClassGroupEnrollmentId = cge.Id) AS PurchasedSessions,
       (SELECT ISNULL(SUM(t.SessionsCount), 0) FROM GroupSessionTransfers t WHERE t.ToClassGroupEnrollmentId = cge.Id) AS TransferredInSessions,
       (SELECT ISNULL(SUM(t.SessionsCount), 0) FROM GroupSessionTransfers t WHERE t.FromClassGroupEnrollmentId = cge.Id) AS TransferredOutSessions,
       (SELECT COUNT(*) FROM SessionAttendance sa WHERE sa.ClassGroupEnrollmentId = cge.Id AND sa.Status IN (1, 2)) AS ConsumedSessions
FROM ClassGroupEnrollments cge
JOIN ClassGroups cg ON cg.Id = cge.ClassGroupId
JOIN Subjects sb ON sb.Id = cg.SubjectId
JOIN AcademicYears ay ON ay.Id = cg.AcademicYearId
WHERE cge.StudentId = @StudentId
  AND cg.AcademicYearId = @AcademicYearId
ORDER BY cge.EnrolledAtUtc DESC;";

        var rows = await connection.QueryAsync<StudentGroupRow>(
            new CommandDefinition(sql, new { StudentId = studentId, AcademicYearId = academicYearId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new StudentGroupEnrollmentItem(
            row.Id, row.ClassGroupId, row.ClassGroupName, row.SubjectName, row.AcademicYearName,
            (EnrollmentStatus)row.Status, row.AgreedUnitPriceCentimes, row.EnrolledAtUtc,
            row.PurchasedSessions, row.ConsumedSessions)
        {
            TransferredInSessions = row.TransferredInSessions,
            TransferredOutSessions = row.TransferredOutSessions
        });
    }

    public async Task<IReadOnlyList<Domain.Enrollments.ClassGroupEnrollment>> GetActiveByAnnualEnrollmentIdAsync(
        int annualEnrollmentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ClassGroupEnrollmentRow>(
            new CommandDefinition($"{SelectColumns} WHERE AnnualEnrollmentId = @AnnualEnrollmentId AND Status = 1;",
                new { AnnualEnrollmentId = annualEnrollmentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToDomain).ToList();
    }

    public async Task<IEnumerable<ClassGroupListItem>> GetTransferTargetsAsync(int groupEnrollmentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // سياق التسجيل الحالي: طالبه + سنته + مستواه/شعبته السنويان
        var context = await connection.QuerySingleOrDefaultAsync<TransferContextRow>(
            new CommandDefinition(@"
SELECT cge.StudentId, cg.AcademicYearId, cge.ClassGroupId, ae.LevelId, ae.StreamId
FROM ClassGroupEnrollments cge
JOIN ClassGroups cg ON cg.Id = cge.ClassGroupId
JOIN AnnualEnrollments ae ON ae.Id = cge.AnnualEnrollmentId
WHERE cge.Id = @Id AND cge.Status = 1;",
                new { Id = groupEnrollmentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (context is null)
            return Array.Empty<ClassGroupListItem>();

        // D-78: المطابقة كاملة في استعلام واحد — مستوى الطالب · فعّال · غير ممتلئ · شعبته ضمن الشعب إن قُيّد (D-59) · ليس مسجلاً فيها
        var sql = EligibleGroupSelect + @"
WHERE cg.AcademicYearId = @YearId
  AND cg.LevelId = @LevelId
  AND cg.IsActive = 1
  AND cg.Id <> @CurrentGroupId
  AND (NOT EXISTS (SELECT 1 FROM ClassGroupStreams cgs WHERE cgs.ClassGroupId = cg.Id)
       OR (@StreamId IS NOT NULL AND EXISTS (SELECT 1 FROM ClassGroupStreams cgs2 WHERE cgs2.ClassGroupId = cg.Id AND cgs2.StreamId = @StreamId)))
  AND (cg.Capacity IS NULL OR (SELECT COUNT(*) FROM ClassGroupEnrollments e WHERE e.ClassGroupId = cg.Id AND e.Status = 1) < cg.Capacity)
  AND NOT EXISTS (SELECT 1 FROM ClassGroupEnrollments e2 WHERE e2.ClassGroupId = cg.Id AND e2.StudentId = @StudentId AND e2.Status = 1)
ORDER BY sb.Name, cg.Name;";

        var rows = await connection.QueryAsync<EligibleGroupRow>(
            new CommandDefinition(sql, new
            {
                YearId = context.AcademicYearId,
                LevelId = context.LevelId,
                CurrentGroupId = context.ClassGroupId,
                StreamId = context.StreamId,
                StudentId = context.StudentId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        return rows.Select(MapToListItem);
    }

    public async Task<IEnumerable<ClassGroupListItem>> GetEnrollableGroupsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-83: المطابقة على أي تسجيل سنوي نشط (تعدد السنوات D-71) — كل فوج يطابق تسجيلاً واحداً كحد أقصى فلا تكرار
        // (الفرادة المفلترة تمنع نشطَين لنفس السنة، وسنة الفوج واحدة) — والشعبة تُفحص على التسجيل المطابِق ذاته (D-59)
        var sql = EligibleGroupSelect + @"
JOIN AnnualEnrollments ae ON ae.StudentId = @StudentId AND ae.Status = 1
                          AND ae.AcademicYearId = cg.AcademicYearId AND ae.LevelId = cg.LevelId
JOIN AcademicYears currentAy ON currentAy.Id = cg.AcademicYearId AND currentAy.IsCurrent = 1
WHERE cg.IsActive = 1
  AND (NOT EXISTS (SELECT 1 FROM ClassGroupStreams cgs WHERE cgs.ClassGroupId = cg.Id)
       OR (ae.StreamId IS NOT NULL AND EXISTS (SELECT 1 FROM ClassGroupStreams cgs2 WHERE cgs2.ClassGroupId = cg.Id AND cgs2.StreamId = ae.StreamId)))
  AND (cg.Capacity IS NULL OR (SELECT COUNT(*) FROM ClassGroupEnrollments e WHERE e.ClassGroupId = cg.Id AND e.Status = 1) < cg.Capacity)
  AND NOT EXISTS (SELECT 1 FROM ClassGroupEnrollments e2 WHERE e2.ClassGroupId = cg.Id AND e2.StudentId = @StudentId AND e2.Status = 1)
ORDER BY ay.StartDate DESC, sb.Name, cg.Name;";

        var rows = await connection.QueryAsync<EligibleGroupRow>(
            new CommandDefinition(sql, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToListItem);
    }

    private static ClassGroupListItem MapToListItem(EligibleGroupRow row) => new(
        row.Id, row.AcademicYearId, row.AcademicYearName,
        row.LevelId, row.LevelName,
        row.SubjectId, row.SubjectName,
        row.TeacherId, row.TeacherFirstName, row.TeacherLastName, row.TeacherFatherName,
        row.RoomId, row.RoomName,
        row.Name, row.Capacity, row.StreamsText, row.IsActive, row.EnrolledCount);

    private static Domain.Enrollments.ClassGroupEnrollment MapToDomain(ClassGroupEnrollmentRow row) =>
        Domain.Enrollments.ClassGroupEnrollment.Load(
            id: row.Id,
            classGroupId: row.ClassGroupId,
            studentId: row.StudentId,
            annualEnrollmentId: row.AnnualEnrollmentId,
            status: (EnrollmentStatus)row.Status,
            snapshotUnitPriceCentimes: row.SnapshotUnitPriceCentimes,
            agreedUnitPriceCentimes: row.AgreedUnitPriceCentimes,
            discountNote: row.DiscountNote,
            enrolledAtUtc: row.EnrolledAtUtc,
            withdrawnAtUtc: row.WithdrawnAtUtc,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}