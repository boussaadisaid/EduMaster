using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>إلغاء مستحق موثق بسبب (D-108) — لا حذف إطلاقاً (D-109)</summary>
public sealed record CancelChargeRequest(int ChargeId, string Reason);

public sealed class CancelChargeHandler
{
    private readonly IChargeRepository _charges;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelChargeHandler> _logger;

    public CancelChargeHandler(IChargeRepository charges, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CancelChargeHandler> logger)
    {
        _charges = charges;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(CancelChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
            return OperationResult.Failure("سبب الإلغاء إلزامي.", ErrorType.Validation);

        try
        {
            var charge = await _charges.GetByIdAsync(request.ChargeId, cancellationToken);
            if (charge is null)
                return OperationResult.Failure("المستحق غير موجود.", ErrorType.NotFound);

            charge.Cancel(request.Reason, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _charges.UpdateAsync(charge, cancellationToken);
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
            _logger.LogError(ex, "Failed to cancel charge {ChargeId}", request.ChargeId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء إلغاء المستحق.", ErrorType.Unexpected);
        }
    }
}