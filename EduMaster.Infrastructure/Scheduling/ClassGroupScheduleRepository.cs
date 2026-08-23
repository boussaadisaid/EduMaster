using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Scheduling;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Scheduling;

public sealed class ClassGroupScheduleRepository : IClassGroupScheduleRepository
{
    private readonly IAdoDbSession _session;

    public ClassGroupScheduleRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجلات = ترتيب أعمدة استعلاماتها حرفياً
    private sealed record ScheduleRow(
        int Id,
        int ClassGroupId,
        byte DayOfWeek,
        TimeSpan StartTime,
        int DurationMinutes,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    private sealed record SlotItemRow(
        int Id,
        int ClassGroupId,
        string GroupName,
        string SubjectName,
        string LevelName,
        string? TeacherFirstName,
        string? TeacherLastName,
        string? TeacherFatherName,
        string? RoomName,
        byte DayOfWeek,
        TimeSpan StartTime,
        int DurationMinutes,
        bool IsActive);

    private sealed record ConflictRow(
        int Id,
        string GroupName,
        string SubjectName,
        string? TeacherFirstName,
        string? TeacherLastName,
        string? TeacherFatherName,
        string? RoomName,
        int? RoomId,
        int? TeacherId,
        byte DayOfWeek,
        TimeSpan StartTime,
        int DurationMinutes);

    private const string SelectColumns = @"
SELECT Id, ClassGroupId, DayOfWeek, StartTime, DurationMinutes, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM ClassGroupSchedules";

    private const string SlotItemSelect = @"
SELECT s.Id, s.ClassGroupId, cg.Name AS GroupName, sb.Name AS SubjectName, l.Name AS LevelName,
       tp.FirstName AS TeacherFirstName, tp.LastName AS TeacherLastName, tp.FatherName AS TeacherFatherName,
       r.Name AS RoomName, s.DayOfWeek, s.StartTime, s.DurationMinutes, s.IsActive
FROM ClassGroupSchedules s
JOIN ClassGroups cg ON cg.Id = s.ClassGroupId
JOIN Subjects sb ON sb.Id = cg.SubjectId
JOIN Levels l ON l.Id = cg.LevelId
LEFT JOIN Teachers t ON t.Id = cg.TeacherId AND t.IsDeleted = 0
LEFT JOIN Persons tp ON tp.Id = t.PersonId AND tp.IsDeleted = 0
LEFT JOIN Rooms r ON r.Id = cg.RoomId";

    public async Task AddAsync(Domain.Scheduling.ClassGroupSchedule schedule, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO ClassGroupSchedules (ClassGroupId, DayOfWeek, StartTime, DurationMinutes, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@ClassGroupId, @DayOfWeek, @StartTime, @DurationMinutes, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                schedule.ClassGroupId,
                DayOfWeek = (byte)schedule.DayOfWeek,
                StartTime = schedule.StartTime.ToTimeSpan(),
                schedule.DurationMinutes,
                schedule.IsActive,
                schedule.CreatedAtUtc,
                schedule.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        schedule.SetId(newId);
    }

    public async Task UpdateAsync(Domain.Scheduling.ClassGroupSchedule schedule, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE ClassGroupSchedules
SET DayOfWeek       = @DayOfWeek,
    StartTime       = @StartTime,
    DurationMinutes = @DurationMinutes,
    IsActive        = @IsActive,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                DayOfWeek = (byte)schedule.DayOfWeek,
                StartTime = schedule.StartTime.ToTimeSpan(),
                schedule.DurationMinutes,
                schedule.IsActive,
                schedule.UpdatedAtUtc,
                schedule.UpdatedByUserId,
                schedule.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"ClassGroupSchedule {schedule.Id} was not found for update.");
    }

    public async Task<Domain.Scheduling.ClassGroupSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ScheduleRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IEnumerable<ScheduleSlotItem>> GetActiveAsync(int? classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الفعّالة منها ولأفواج فعّالة فقط — مرتبة باليوم ثم الساعة (مصدر التوليد D-87)
        var rows = await connection.QueryAsync<SlotItemRow>(
            new CommandDefinition(SlotItemSelect + @"
WHERE s.IsActive = 1 AND cg.IsActive = 1
  AND (@GroupId IS NULL OR s.ClassGroupId = @GroupId)
ORDER BY s.DayOfWeek, s.StartTime;",
                new { GroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToSlotItem);
    }

    public async Task<IEnumerable<ScheduleSlotItem>> GetForTimetableAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // الجدول: لأفواج فعّالة — والمعطّلة تُدرج عند الطلب لتُفعَّل منها (D-86)
        var rows = await connection.QueryAsync<SlotItemRow>(
            new CommandDefinition(SlotItemSelect + @"
WHERE cg.IsActive = 1
  AND (@IncludeInactive = 1 OR s.IsActive = 1)
ORDER BY s.DayOfWeek, s.StartTime;",
                new { IncludeInactive = includeInactive },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToSlotItem);
    }

    public async Task<IEnumerable<ScheduleSlotItem>> GetForGroupAsync(int classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SlotItemRow>(
            new CommandDefinition(SlotItemSelect + @"
WHERE s.ClassGroupId = @GroupId
ORDER BY s.DayOfWeek, s.StartTime;",
                new { GroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(MapToSlotItem);
    }

    public async Task<IEnumerable<ScheduleConflictItem>> FindConflictsAsync(int dayOfWeek, TimeSpan startTime, int durationMinutes,
        int? roomId, int? teacherId, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-89: تقاطع المدىين الزمنيين في نفس اليوم + نفس القاعة أو الأستاذ — تحذير غير مانع
        const string sql = @"
SELECT s.Id, cg.Name AS GroupName, sb.Name AS SubjectName,
       tp.FirstName AS TeacherFirstName, tp.LastName AS TeacherLastName, tp.FatherName AS TeacherFatherName,
       r.Name AS RoomName, cg.RoomId, cg.TeacherId, s.DayOfWeek, s.StartTime, s.DurationMinutes
FROM ClassGroupSchedules s
JOIN ClassGroups cg ON cg.Id = s.ClassGroupId
JOIN Subjects sb ON sb.Id = cg.SubjectId
LEFT JOIN Teachers t ON t.Id = cg.TeacherId AND t.IsDeleted = 0
LEFT JOIN Persons tp ON tp.Id = t.PersonId AND tp.IsDeleted = 0
LEFT JOIN Rooms r ON r.Id = cg.RoomId
WHERE s.IsActive = 1 AND cg.IsActive = 1
  AND s.DayOfWeek = @DayOfWeek
  AND (@ExcludeId IS NULL OR s.Id <> @ExcludeId)
  AND ((@RoomId IS NOT NULL AND cg.RoomId = @RoomId) OR (@TeacherId IS NOT NULL AND cg.TeacherId = @TeacherId))
  AND DATEDIFF(MINUTE, CAST('00:00' AS TIME), s.StartTime) < @StartMinutes + @DurationMinutes
  AND @StartMinutes < DATEDIFF(MINUTE, CAST('00:00' AS TIME), s.StartTime) + s.DurationMinutes;";

        var rows = await connection.QueryAsync<ConflictRow>(
            new CommandDefinition(sql, new
            {
                DayOfWeek = (byte)dayOfWeek,
                ExcludeId = excludeId,
                RoomId = roomId,
                TeacherId = teacherId,
                StartMinutes = (int)startTime.TotalMinutes,
                DurationMinutes = durationMinutes
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        return rows.Select(row =>
        {
            var roomMatched = roomId is not null && row.RoomId == roomId;
            var teacherMatched = teacherId is not null && row.TeacherId == teacherId;
            var reason = roomMatched && teacherMatched ? "القاعة والأستاذ" : roomMatched ? "القاعة" : "الأستاذ";
            var teacherName = string.IsNullOrWhiteSpace(row.TeacherFirstName)
                ? null
                : string.Join(" ", new[] { row.TeacherFirstName, row.TeacherLastName, row.TeacherFatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));

            return new ScheduleConflictItem(
                row.GroupName, row.SubjectName, row.DayOfWeek, TimeOnly.FromTimeSpan(row.StartTime), row.DurationMinutes,
                row.RoomName, teacherName, reason);
        });
    }

    private static ScheduleSlotItem MapToSlotItem(SlotItemRow row) => new(
        row.Id, row.ClassGroupId, row.GroupName, row.SubjectName, row.LevelName,
        row.TeacherFirstName, row.TeacherLastName, row.TeacherFatherName,
        row.RoomName, row.DayOfWeek, TimeOnly.FromTimeSpan(row.StartTime), row.DurationMinutes, row.IsActive);

    private static Domain.Scheduling.ClassGroupSchedule MapToDomain(ScheduleRow row) =>
        Domain.Scheduling.ClassGroupSchedule.Load(
            id: row.Id,
            classGroupId: row.ClassGroupId,
            dayOfWeek: row.DayOfWeek,
            startTime: TimeOnly.FromTimeSpan(row.StartTime),
            durationMinutes: row.DurationMinutes,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}