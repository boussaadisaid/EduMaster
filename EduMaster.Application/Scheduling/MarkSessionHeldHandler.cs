using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>إقامة حصة — تفتح الحضور في 3.3 (D-90) · الملغاة لا تُقام</summary>
public sealed record MarkSessionHeldRequest(int SessionId);

public sealed class MarkSessionHeldHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkSessionHeldHandler> _logger;

    public MarkSessionHeldHandler(IClassSessionRepository sessions, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<MarkSessionHeldHandler> logger)
    {
        _sessions = sessions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(MarkSessionHeldRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var session = await _sessions.GetByIdAsync(request.SessionId, cancellationToken);
            if (session is null)
                return OperationResult.Failure("الحصة غير موجودة.", ErrorType.NotFound);

            session.MarkHeld(_clock.UtcNow, _currentUser.UserAccountId);

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
            _logger.LogError(ex, "Failed to mark session {SessionId} as held", request.SessionId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تثبيت إقامة الحصة.", ErrorType.Unexpected);
        }
    }
}