using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>
/// تصحيح لقطة أستاذ حصة مُقامة بلقطة فارغة (6.6-ص-ب — مراجعة الأجور 2026-08-27:
/// UpdateAsync لم يكن يكتب TeacherId فضاعت اللقطات الملتقطة منذ 5.1 — أُصلح السطر المفقود).
/// الحُراس: حصة قائمة ومُقامة ولقطتها فارغة · أستاذ قائم — والكيان يمنع إعادة كتابة اللقطات القائمة أبداً.
/// </summary>
public sealed record CorrectSessionTeacherRequest(int SessionId, int TeacherId);

public sealed class CorrectSessionTeacherHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly ITeacherRepository _teachers;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CorrectSessionTeacherHandler> _logger;

    public CorrectSessionTeacherHandler(IClassSessionRepository sessions, ITeacherRepository teachers, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<CorrectSessionTeacherHandler> logger)
    {
        _sessions = sessions;
        _teachers = teachers;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(CorrectSessionTeacherRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var session = await _sessions.GetByIdAsync(request.SessionId, cancellationToken);
            if (session is null)
                return OperationResult.Failure("الحصة غير موجودة.", ErrorType.NotFound);

            var teacher = await _teachers.GetByIdAsync(request.TeacherId, cancellationToken);
            if (teacher is null)
                return OperationResult.Failure("الأستاذ المحدد غير موجود.", ErrorType.Validation);

            // حُراس الكيان: مُقامة فقط · لقطة فارغة فقط (القائمة لا تُعاد كتابتها) · معرّف صالح
            session.CorrectHeldTeacherSnapshot(request.TeacherId, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _sessions.UpdateAsync(session, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to correct teacher snapshot for session {SessionId} to teacher {TeacherId}",
                request.SessionId, request.TeacherId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تصحيح لقطة الأستاذ.", ErrorType.Unexpected);
        }
    }
}
