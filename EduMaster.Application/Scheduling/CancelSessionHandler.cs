using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>إلغاء حصة (عطلة/ظرف) — الملغاة لا تخصم شيئاً (D-90) · المُقامة لا تُلغى</summary>
public sealed record CancelSessionRequest(int SessionId);

public sealed class CancelSessionHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelSessionHandler> _logger;

    public CancelSessionHandler(IClassSessionRepository sessions, IAcademicYearRepository academicYears, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<CancelSessionHandler> logger)
    {
        _sessions = sessions;
        _academicYears = academicYears;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(CancelSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var session = await _sessions.GetByIdForAcademicYearAsync(request.SessionId, currentYear.Id, cancellationToken);

            if (session is null)
                return OperationResult.Failure("الحصة غير موجودة.", ErrorType.NotFound);

            session.Cancel(_clock.UtcNow, _currentUser.UserAccountId);

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
            _logger.LogError(ex, "Failed to cancel session {SessionId}", request.SessionId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء إلغاء الحصة.", ErrorType.Unexpected);
        }
    }

}