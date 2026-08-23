using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Enums;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Scheduling;

public sealed class SessionAttendanceRepository : ISessionAttendanceRepository
{
    private readonly IAdoDbSession _session;

    public SessionAttendanceRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجل = ترتيب أعمدة الـSELECT حرفياً
    private sealed record AttendanceMarkRow(int ClassGroupEnrollmentId, byte Status, string? Note);

    public async Task AddAsync(Domain.Scheduling.SessionAttendance attendance, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO SessionAttendance (ClassSessionId, ClassGroupEnrollmentId, Status, Note, MarkedAtUtc, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@ClassSessionId, @ClassGroupEnrollmentId, @Status, @Note, @MarkedAtUtc, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                attendance.ClassSessionId,
                attendance.ClassGroupEnrollmentId,
                Status = (byte)attendance.Status,
                attendance.Note,
                attendance.MarkedAtUtc,
                attendance.CreatedAtUtc,
                attendance.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        attendance.SetId(newId);
    }

    public async Task<int> DeleteForSessionAsync(int classSessionId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        return await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM SessionAttendance WHERE ClassSessionId = @ClassSessionId;",
                new { ClassSessionId = classSessionId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<SessionAttendanceMarkItem>> GetMarksForSessionAsync(int classSessionId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<AttendanceMarkRow>(
            new CommandDefinition(@"
SELECT ClassGroupEnrollmentId, Status, Note
FROM SessionAttendance
WHERE ClassSessionId = @ClassSessionId;",
                new { ClassSessionId = classSessionId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new SessionAttendanceMarkItem(
            row.ClassGroupEnrollmentId, (AttendanceStatus)row.Status, row.Note));
    }
}