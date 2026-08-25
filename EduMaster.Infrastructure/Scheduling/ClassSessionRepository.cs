using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Scheduling;

public sealed class ClassSessionRepository : IClassSessionRepository
{
    private readonly IAdoDbSession _session;

    public ClassSessionRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجلات = ترتيب أعمدة استعلاماتها حرفياً
    private sealed record ClassSessionRow(
        int Id,
        int ClassGroupId,
        int? SourceScheduleId,
         int? TeacherId,
        DateTime StartsAt,
        int DurationMinutes,
        byte Status,
        string? Topic,
        DateTime? CancelledAtUtc,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private sealed record SessionListRow(
        int Id,
        int ClassGroupId,
        string GroupName,
        string SubjectName,
        string LevelName,
        string? TeacherFirstName,
        string? TeacherLastName,
        string? TeacherFatherName,
        string? RoomName,
        DateTime StartsAt,
        int DurationMinutes,
        byte Status,
        string? Topic,
        bool IsAdHoc,
        int ActiveEnrolledCount);

    private const string SelectColumns = @"
SELECT Id, ClassGroupId, SourceScheduleId, TeacherId, StartsAt, DurationMinutes, Status, Topic, CancelledAtUtc,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM ClassSessions";

    public async Task AddAsync(Domain.Scheduling.ClassSession session, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO ClassSessions (ClassGroupId, SourceScheduleId, StartsAt, DurationMinutes, Status, Topic, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@ClassGroupId, @SourceScheduleId, @StartsAt, @DurationMinutes, @Status, @Topic, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                session.ClassGroupId,
                session.SourceScheduleId,
                session.StartsAt,
                session.DurationMinutes,
                Status = (byte)session.Status,
                session.Topic,
                session.CreatedAtUtc,
                session.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        session.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Scheduling.ClassSession session, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الهوية الزمنية والمصدر ثوابت — التحديث للحالة والموضوع والإلغاء
        const string sql = @"
UPDATE ClassSessions
SET Status          = @Status,
    Topic           = @Topic,
    CancelledAtUtc  = @CancelledAtUtc,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                Status = (byte)session.Status,
                session.Topic,
                session.CancelledAtUtc,
                session.UpdatedAtUtc,
                session.UpdatedByUserId,
                session.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"ClassSession {session.Id} was not found for update.");
    }

    public async Task<Domain.Scheduling.ClassSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClassSessionRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IEnumerable<ClassSessionListItem>> GetByDateRangeAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح (D-40) + عدد النشطين المتوقع حضورهم
        const string sql = @"
SELECT cs.Id, cs.ClassGroupId, cg.Name AS GroupName, sb.Name AS SubjectName, l.Name AS LevelName,
       tp.FirstName AS TeacherFirstName, tp.LastName AS TeacherLastName, tp.FatherName AS TeacherFatherName,
       r.Name AS RoomName, cs.StartsAt, cs.DurationMinutes, cs.Status, cs.Topic,
       CASE WHEN cs.SourceScheduleId IS NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsAdHoc,
       (SELECT COUNT(*) FROM ClassGroupEnrollments e WHERE e.ClassGroupId = cs.ClassGroupId AND e.Status = 1) AS ActiveEnrolledCount
FROM ClassSessions cs
JOIN ClassGroups cg ON cg.Id = cs.ClassGroupId
JOIN Subjects sb ON sb.Id = cg.SubjectId
JOIN Levels l ON l.Id = cg.LevelId
LEFT JOIN Teachers t ON t.Id = cg.TeacherId AND t.IsDeleted = 0
LEFT JOIN Persons tp ON tp.Id = t.PersonId AND tp.IsDeleted = 0
LEFT JOIN Rooms r ON r.Id = cg.RoomId
WHERE cs.StartsAt >= @From AND cs.StartsAt < @ToExclusive
  AND (@GroupId IS NULL OR cs.ClassGroupId = @GroupId)
ORDER BY cs.StartsAt, cg.Name;";

        var rows = await connection.QueryAsync<SessionListRow>(
            new CommandDefinition(sql, new { From = from, ToExclusive = toExclusive, GroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new ClassSessionListItem(
            row.Id, row.ClassGroupId, row.GroupName, row.SubjectName, row.LevelName,
            row.TeacherFirstName, row.TeacherLastName, row.TeacherFatherName, row.RoomName,
            row.StartsAt, row.DurationMinutes, (SessionStatus)row.Status, row.Topic,
            row.IsAdHoc, row.ActiveEnrolledCount));
    }

    public async Task<bool> AnyExistsAtAsync(int classGroupId, DateTime startsAt, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"SELECT COUNT(*) FROM ClassSessions
                  WHERE ClassGroupId = @ClassGroupId AND StartsAt = @StartsAt AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { ClassGroupId = classGroupId, StartsAt = startsAt, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IReadOnlyCollection<DateTime>> GetSessionStartsAsync(int classGroupId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var starts = await connection.QueryAsync<DateTime>(
            new CommandDefinition(
                @"SELECT StartsAt FROM ClassSessions
                  WHERE ClassGroupId = @ClassGroupId AND StartsAt >= @From AND StartsAt < @ToExclusive;",
                new { ClassGroupId = classGroupId, From = from, ToExclusive = toExclusive },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return starts.ToList();
    }

    public async Task<int> CancelFutureScheduledBySlotAsync(int scheduleId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-88: المجدولة المستقبلية فقط — المُقامة والملغاة لا تُمسّان
        return await connection.ExecuteAsync(
            new CommandDefinition(@"
UPDATE ClassSessions
SET Status = 3, CancelledAtUtc = @UtcNow, UpdatedAtUtc = @UtcNow, UpdatedByUserId = @UpdatedByUserId
WHERE SourceScheduleId = @ScheduleId AND Status = 1 AND StartsAt > @LocalNow;",
                new { ScheduleId = scheduleId, LocalNow = localNow, UtcNow = utcNow, UpdatedByUserId = updatedByUserId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    public async Task<int> CancelFutureScheduledByGroupAsync(int classGroupId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-90: مع تعطيل الفوج — المجدولة المستقبلية فقط
        return await connection.ExecuteAsync(
            new CommandDefinition(@"
UPDATE ClassSessions
SET Status = 3, CancelledAtUtc = @UtcNow, UpdatedAtUtc = @UtcNow, UpdatedByUserId = @UpdatedByUserId
WHERE ClassGroupId = @ClassGroupId AND Status = 1 AND StartsAt > @LocalNow;",
                new { ClassGroupId = classGroupId, LocalNow = localNow, UtcNow = utcNow, UpdatedByUserId = updatedByUserId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    private static Domain.Scheduling.ClassSession MapToDomain(ClassSessionRow row) =>
        Domain.Scheduling.ClassSession.Load(
            id: row.Id,
            classGroupId: row.ClassGroupId,
            sourceScheduleId: row.SourceScheduleId,
            teacherId: row.TeacherId,
            startsAt: row.StartsAt,
            durationMinutes: row.DurationMinutes,
            status: (SessionStatus)row.Status,
            topic: row.Topic,
            cancelledAtUtc: row.CancelledAtUtc,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}