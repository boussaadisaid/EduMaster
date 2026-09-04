using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>
/// تركيب ديالوغ الحضور (D-94): مسجَّلو الفوج النشطون (D-102) ← كلٌّ بعلامته المحفوظة أو الافتراضي «حاضر».
/// على المُقامة فقط (D-100) — قراءة بلا معاملة.
/// </summary>
public sealed class GetSessionAttendanceHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly ISessionAttendanceRepository _attendance;
    private readonly ILogger<GetSessionAttendanceHandler> _logger;

    public GetSessionAttendanceHandler(IClassSessionRepository sessions, IAcademicYearRepository academicYears, IClassGroupEnrollmentRepository groupEnrollments,
        ISessionAttendanceRepository attendance, ILogger<GetSessionAttendanceHandler> logger)
    {
        _sessions = sessions;
        _academicYears = academicYears;
        _groupEnrollments = groupEnrollments;
        _attendance = attendance;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<AttendanceRosterItem>>> ExecuteAsync(int classSessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult<IReadOnlyList<AttendanceRosterItem>>.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var session = await _sessions.GetByIdForAcademicYearAsync(classSessionId, currentYear.Id, cancellationToken);
            if (session is null)
                return OperationResult<IReadOnlyList<AttendanceRosterItem>>.Failure("الحصة غير موجودة.", ErrorType.NotFound);

            // D-100: الحضور على المُقامة فقط — حضور ⇒ إقامة مسجلة
            if (session.Status != SessionStatus.Held)
                return OperationResult<IReadOnlyList<AttendanceRosterItem>>.Failure(
                    "الحضور يُسجَّل لحصة مُقامة فقط — مرّر «أُقيمت» أولاً.", ErrorType.BusinessRule);

            // D-102: قائمة الديالوغ = النشطون الآن — المنسحب يحتفظ بتاريخه ولا يظهر لتحديد جديد
            var roster = await _groupEnrollments.GetForGroupAsync(session.ClassGroupId, cancellationToken);
            var activeRoster = roster.Where(r => r.Status == EnrollmentStatus.Active).ToList();

            var marks = await _attendance.GetMarksForSessionAsync(classSessionId, cancellationToken);
            var markByEnrollment = marks.ToDictionary(m => m.ClassGroupEnrollmentId);

            var items = activeRoster.Select(r =>
            {
                markByEnrollment.TryGetValue(r.Id, out var mark);
                return new AttendanceRosterItem(
                    r.Id,
                    r.StudentId,
                    r.FullName,
                    mark?.Status ?? AttendanceStatus.Present,   // D-94: الحاضر هو القاعدة
                    mark?.Note);
            }).ToList();

            return OperationResult<IReadOnlyList<AttendanceRosterItem>>.Success(items);
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load attendance roster for session {ClassSessionId}", classSessionId);
            return OperationResult<IReadOnlyList<AttendanceRosterItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل قائمة الحضور.", ErrorType.Unexpected);
        }
    }
}