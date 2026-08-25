using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Payroll;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Payroll;

/// <summary>
/// منفذ قراءات الاحتساب (5.2) — قراءات مسطّحة خام عبر الجداول (روح D-40):
/// الحصص المُقامة بلقطة أستاذها (D-117 — <b>بما فيها اللقطة الفارغة</b>: تُذكر في التحذيرات ولا تختفي بصمت) +
/// حضورها بالسعر المتفق لكل صاحب علامة (D-52) + أيام العمل + السياسات الفعّالة.
/// المجالان شاملان من الطرفين — يُترجمان هنا إلى [من 00:00 ، إلى+1 00:00) لأن StartsAt بوقت.
/// </summary>
public sealed class PayrollFactsRepository : IPayrollFactsRepository
{
    private readonly IAdoDbSession _session;

    public PayrollFactsRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: ترتيب السجلات = ترتيب أعمدة الـSELECT حرفياً
    private sealed record PolicyRow(int Id, byte PayeeKind, int? TeacherId, int? EmployeeId, int? ClassGroupId,
        byte Kind, long RateCentimes, decimal? Percentage, bool CountsUnjustifiedAbsent, bool IsActive,
        DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? UpdatedAtUtc, int? UpdatedByUserId);

    private sealed record SessionRow(int Id, int ClassGroupId, string GroupName, int? TeacherId, DateTime StartsAt, int DurationMinutes);
    private sealed record AttendanceRow(int ClassSessionId, byte Status, long AgreedUnitPriceCentimes);
    private sealed record WorkDayRow(int EmployeeId, DateTime WorkDate);

    public async Task<IReadOnlyList<PayPolicy>> GetAllActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PolicyRow>(
            new CommandDefinition(@"
SELECT Id, PayeeKind, TeacherId, EmployeeId, ClassGroupId, Kind, RateCentimes, Percentage, CountsUnjustifiedAbsent, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM dbo.PayPolicies
WHERE IsActive = 1;",
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(r => PayPolicy.Load(
            r.Id, (PayeeKind)r.PayeeKind, r.TeacherId, r.EmployeeId, r.ClassGroupId,
            (PayPolicyKind)r.Kind, r.RateCentimes, r.Percentage, r.CountsUnjustifiedAbsent, r.IsActive,
            r.CreatedAtUtc, r.CreatedByUserId, r.UpdatedAtUtc, r.UpdatedByUserId)).ToList();
    }

    public async Task<IReadOnlyList<PayrollSessionFact>> GetHeldSessionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SessionRow>(
            new CommandDefinition(@"
SELECT s.Id, s.ClassGroupId, g.Name AS GroupName, s.TeacherId, s.StartsAt, s.DurationMinutes
FROM dbo.ClassSessions s
INNER JOIN dbo.ClassGroups g ON g.Id = s.ClassGroupId
WHERE s.Status = 2                      -- مُقامة فقط (Held)
  AND s.StartsAt >= @From AND s.StartsAt < @ToExclusive;",
                new { From = from.ToDateTime(TimeOnly.MinValue), ToExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue) },
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(r => new PayrollSessionFact(r.Id, r.ClassGroupId, r.GroupName, r.TeacherId, r.StartsAt, r.DurationMinutes)).ToList();
    }

    public async Task<IReadOnlyList<PayrollAttendanceFact>> GetAttendanceFactsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<AttendanceRow>(
            new CommandDefinition(@"
SELECT a.ClassSessionId, a.Status, e.AgreedUnitPriceCentimes
FROM dbo.SessionAttendance a
INNER JOIN dbo.ClassSessions s ON s.Id = a.ClassSessionId
INNER JOIN dbo.ClassGroupEnrollments e ON e.Id = a.ClassGroupEnrollmentId   -- لقطة D-52: سعر المتفق من تسجيل صاحب العلامة
WHERE s.Status = 2
  AND s.StartsAt >= @From AND s.StartsAt < @ToExclusive;",
                new { From = from.ToDateTime(TimeOnly.MinValue), ToExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue) },
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(r => new PayrollAttendanceFact(r.ClassSessionId, (AttendanceStatus)r.Status, r.AgreedUnitPriceCentimes)).ToList();
    }

    public async Task<IReadOnlyList<PayrollWorkDayFact>> GetWorkDayFactsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<WorkDayRow>(
            new CommandDefinition(@"
SELECT EmployeeId, WorkDate
FROM dbo.EmployeeWorkLog
WHERE WorkDate >= @From AND WorkDate <= @To;",
                new { From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) },
                transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));

        return rows.Select(r => new PayrollWorkDayFact(r.EmployeeId, DateOnly.FromDateTime(r.WorkDate))).ToList();
    }
}