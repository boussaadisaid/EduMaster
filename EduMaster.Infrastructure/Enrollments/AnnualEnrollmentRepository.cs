using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Enrollments;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Enrollments;

public sealed class AnnualEnrollmentRepository : IAnnualEnrollmentRepository
{
    private readonly IAdoDbSession _session;

    public AnnualEnrollmentRepository(IAdoDbSession session) => _session = session;

    private sealed record AnnualEnrollmentRow(
        int Id,
        int StudentId,
        int AcademicYearId,
        int LevelId,
        int? StreamId,
        byte Status,
        long AgreedRegistrationFeeCentimes,
        string? RegistrationFeeNote,
        DateTime EnrolledAtUtc,
        DateTime? WithdrawnAtUtc,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private sealed record AnnualEnrollmentListRow(
        int Id,
        int AcademicYearId,
        string AcademicYearName,
        int LevelId,
        string LevelName,
        int? StreamId,
        string? StreamName,
        byte Status,
        long AgreedRegistrationFeeCentimes,
        string? RegistrationFeeNote,
        DateTime EnrolledAtUtc,
        DateTime? WithdrawnAtUtc);

    // ⚠ D-81: بترتيب أعمدة SELECT حرفياً
    private sealed record RolloverCandidateRow(
        int StudentId,
        string FullName,
        string? Phone,
        int SourceLevelId,
        string SourceLevelName,
        int? SourceStreamId,
        string? SourceStreamName,
        int AlreadyInTarget,
        bool PersonActive,
        bool FileDeleted,
        long DebtCentimes);

    private const string SelectColumns = @"
SELECT Id, StudentId, AcademicYearId, LevelId, StreamId, Status, AgreedRegistrationFeeCentimes, RegistrationFeeNote,
       EnrolledAtUtc, WithdrawnAtUtc, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM AnnualEnrollments";

    public async Task AddAsync(Domain.Enrollments.AnnualEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO AnnualEnrollments
    (StudentId, AcademicYearId, LevelId, StreamId, Status, AgreedRegistrationFeeCentimes, RegistrationFeeNote,
     EnrolledAtUtc, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES
    (@StudentId, @AcademicYearId, @LevelId, @StreamId, @Status, @AgreedRegistrationFeeCentimes, @RegistrationFeeNote,
     @EnrolledAtUtc, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                enrollment.StudentId,
                enrollment.AcademicYearId,
                enrollment.LevelId,
                enrollment.StreamId,
                Status = (byte)enrollment.Status,
                enrollment.AgreedRegistrationFeeCentimes,
                enrollment.RegistrationFeeNote,
                enrollment.EnrolledAtUtc,
                enrollment.CreatedAtUtc,
                enrollment.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        enrollment.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Enrollments.AnnualEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // StudentId/AcademicYearId/EnrolledAtUtc ثوابت هوية — لا تُحدَّث أبداً (D-72)
        const string sql = @"
UPDATE AnnualEnrollments
SET LevelId                       = @LevelId,
    StreamId                      = @StreamId,
    Status                        = @Status,
    AgreedRegistrationFeeCentimes = @AgreedRegistrationFeeCentimes,
    RegistrationFeeNote           = @RegistrationFeeNote,
    WithdrawnAtUtc                = @WithdrawnAtUtc,
    UpdatedAtUtc                  = @UpdatedAtUtc,
    UpdatedByUserId               = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                enrollment.LevelId,
                enrollment.StreamId,
                Status = (byte)enrollment.Status,
                enrollment.AgreedRegistrationFeeCentimes,
                enrollment.RegistrationFeeNote,
                enrollment.WithdrawnAtUtc,
                enrollment.UpdatedAtUtc,
                enrollment.UpdatedByUserId,
                enrollment.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"AnnualEnrollment {enrollment.Id} was not found for update.");
    }

    public async Task<Domain.Enrollments.AnnualEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<AnnualEnrollmentRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM AnnualEnrollments WHERE StudentId = @StudentId AND AcademicYearId = @AcademicYearId AND Status = 1;",
                new { StudentId = studentId, AcademicYearId = academicYearId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<Domain.Enrollments.AnnualEnrollment?> GetActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الفهرس المفلتر UX_AnnualEnrollments_Student_Year_Active يضمن صفاً واحداً كحد أقصى — QuerySingleOrDefault آمنة
        var row = await connection.QuerySingleOrDefaultAsync<AnnualEnrollmentRow>(
            new CommandDefinition($"{SelectColumns} WHERE StudentId = @StudentId AND AcademicYearId = @AcademicYearId AND Status = 1;",
                new { StudentId = studentId, AcademicYearId = academicYearId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IEnumerable<AnnualEnrollmentListItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح (D-40) — أسماء السنة/المستوى/الشعبة عبر JOIN — الأحدث أولاً
        const string sql = @"
SELECT ae.Id, ae.AcademicYearId, ay.Name AS AcademicYearName,
       ae.LevelId, l.Name AS LevelName,
       ae.StreamId, s.Name AS StreamName,
       ae.Status, ae.AgreedRegistrationFeeCentimes, ae.RegistrationFeeNote,
       ae.EnrolledAtUtc, ae.WithdrawnAtUtc
FROM AnnualEnrollments ae
JOIN AcademicYears ay ON ay.Id = ae.AcademicYearId
JOIN Levels l ON l.Id = ae.LevelId
LEFT JOIN Streams s ON s.Id = ae.StreamId
WHERE ae.StudentId = @StudentId
ORDER BY ae.EnrolledAtUtc DESC;";

        var rows = await connection.QueryAsync<AnnualEnrollmentListRow>(
            new CommandDefinition(sql, new { StudentId = studentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new AnnualEnrollmentListItem(
            row.Id, row.AcademicYearId, row.AcademicYearName,
            row.LevelId, row.LevelName,
            row.StreamId, row.StreamName,
            (EnrollmentStatus)row.Status,
            row.AgreedRegistrationFeeCentimes, row.RegistrationFeeNote,
            row.EnrolledAtUtc, row.WithdrawnAtUtc));
    }

    public async Task<bool> HasActiveGroupEnrollmentsAsync(int annualEnrollmentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-54/D-72 (مفعَّل منذ 2.4): أفواج نشطة تمنع تغيير المستوى/الشعبة للتسجيل السنوي
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM ClassGroupEnrollments WHERE AnnualEnrollmentId = @Id AND Status = 1;",
                new { Id = annualEnrollmentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IReadOnlyList<RolloverCandidateItem>> GetRolloverCandidatesAsync(int sourceYearId, int targetYearId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // 6.2 (D-129): نشطو المصدر كلهم — الأهلية والسبب يُحسبان عند التجسيد (مرئي لا صامت روح D-124)
        // الدين معلوماتي لا مانع (تر-5): Σ(الحالي − مخصوص) على المستحقات الفعّالة (D-109) · «في الهدف مسبقاً» يُقرأ بضمّة ذاتية على النشط
        const string sql = @"
SELECT ae.StudentId,
       CONCAT_WS(N' ', p.FirstName, p.LastName, p.FatherName) AS FullName,
       p.Phone,
       ae.LevelId AS SourceLevelId, l.Name AS SourceLevelName,
       ae.StreamId AS SourceStreamId, st.Name AS SourceStreamName,
       CASE WHEN tgt.Id IS NOT NULL THEN 1 ELSE 0 END AS AlreadyInTarget,
       p.IsActive AS PersonActive,
       s.IsDeleted AS FileDeleted,
       ISNULL(debt.RemainingCentimes, 0) AS DebtCentimes
FROM AnnualEnrollments ae
JOIN Students s ON s.Id = ae.StudentId
JOIN Persons p ON p.Id = s.PersonId
JOIN Levels l ON l.Id = ae.LevelId
LEFT JOIN Streams st ON st.Id = ae.StreamId
LEFT JOIN AnnualEnrollments tgt
       ON tgt.StudentId = ae.StudentId AND tgt.AcademicYearId = @TargetYearId AND tgt.Status = 1
LEFT JOIN (SELECT c.StudentId, SUM(c.AmountCentimes - ISNULL(alloc.SumAllocated, 0)) AS RemainingCentimes
           FROM Charges c
           LEFT JOIN (SELECT ChargeId, SUM(AmountCentimes) AS SumAllocated
                      FROM PaymentAllocations GROUP BY ChargeId) alloc
                  ON alloc.ChargeId = c.Id
           WHERE c.Status = 1
           GROUP BY c.StudentId) debt
       ON debt.StudentId = ae.StudentId
WHERE ae.AcademicYearId = @SourceYearId AND ae.Status = 1
ORDER BY l.Name, st.Name, FullName;";

        var rows = await connection.QueryAsync<RolloverCandidateRow>(
            new CommandDefinition(sql, new { SourceYearId = sourceYearId, TargetYearId = targetYearId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row =>
        {
            // الأهلية (تر-6): شخص فعّال وملف غير محذوف — والسبب مرئي
            var isEligible = row.PersonActive && !row.FileDeleted;
            var reason = !row.PersonActive ? "شخصه معطّل — فعّله من شاشة الأشخاص أولاً"
                       : row.FileDeleted ? "ملفه محذوف — راجع ملفه أو أعد إنشاءه"
                       : null;
            return new RolloverCandidateItem(
                row.StudentId, row.FullName, row.Phone,
                row.SourceLevelId, row.SourceLevelName,
                row.SourceStreamId, row.SourceStreamName,
                row.AlreadyInTarget == 1,
                isEligible, reason,
                row.DebtCentimes);
        }).ToList();
    }

    private static Domain.Enrollments.AnnualEnrollment MapToDomain(AnnualEnrollmentRow row) =>
        Domain.Enrollments.AnnualEnrollment.Load(
            id: row.Id,
            studentId: row.StudentId,
            academicYearId: row.AcademicYearId,
            levelId: row.LevelId,
            streamId: row.StreamId,
            status: (EnrollmentStatus)row.Status,
            agreedRegistrationFeeCentimes: row.AgreedRegistrationFeeCentimes,
            registrationFeeNote: row.RegistrationFeeNote,
            enrolledAtUtc: row.EnrolledAtUtc,
            withdrawnAtUtc: row.WithdrawnAtUtc,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}