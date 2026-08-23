using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>
/// حفظ حضور حصة (D-94): استبدال ذرّي كامل في معاملة واحدة (D-101 — تصحيح مسموح، المخصوم يُعاد حسابه تلقائياً).
/// الحُراس: حصة مُقامة (D-100) · كل سطر يتبع مسجَّلاً نشطاً في فوج الحصة (D-102 خلفياً).
/// </summary>
public sealed record SaveSessionAttendanceRequest(int ClassSessionId, IReadOnlyList<SessionAttendanceEntry> Entries);

public sealed class SaveSessionAttendanceHandler
{
    private readonly ISessionAttendanceRepository _attendance;
    private readonly IClassSessionRepository _sessions;
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SaveSessionAttendanceHandler> _logger;

    public SaveSessionAttendanceHandler(ISessionAttendanceRepository attendance, IClassSessionRepository sessions,
        IClassGroupEnrollmentRepository groupEnrollments, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<SaveSessionAttendanceHandler> logger)
    {
        _attendance = attendance;
        _sessions = sessions;
        _groupEnrollments = groupEnrollments;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(SaveSessionAttendanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var session = await _sessions.GetByIdAsync(request.ClassSessionId, cancellationToken);
            if (session is null)
                return OperationResult<int>.Failure("الحصة غير موجودة.", ErrorType.NotFound);

            // D-100: حضور على المُقامة فقط
            if (session.Status != SessionStatus.Held)
                return OperationResult<int>.Failure("الحضور يُسجَّل لحصة مُقامة فقط — مرّر «أُقيمت» أولاً.", ErrorType.BusinessRule);

            // D-102 خلفياً: كل سطر يتبع مسجَّلاً نشطاً في فوج الحصة
            var roster = await _groupEnrollments.GetForGroupAsync(session.ClassGroupId, cancellationToken);
            var activeEnrollmentIds = roster.Where(r => r.Status == EnrollmentStatus.Active).Select(r => r.Id).ToHashSet();
            if (request.Entries.Any(e => !activeEnrollmentIds.Contains(e.ClassGroupEnrollmentId)))
                return OperationResult<int>.Failure("سطر حضور لا يتبع مسجَّلاً نشطاً في فوج هذه الحصة — أعد فتح الديالوغ.", ErrorType.Validation);

            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            var entities = request.Entries
                .Select(e => Domain.Scheduling.SessionAttendance.Create(
                    request.ClassSessionId, e.ClassGroupEnrollmentId, e.Status, e.Note, utcNow, userId))
                .ToList();

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _attendance.DeleteForSessionAsync(request.ClassSessionId, cancellationToken);   // D-101: استبدال ذرّي
            foreach (var entity in entities)
                await _attendance.AddAsync(entity, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(entities.Count);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to save attendance for session {ClassSessionId}", request.ClassSessionId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء حفظ الحضور.", ErrorType.Unexpected);
        }
    }
}