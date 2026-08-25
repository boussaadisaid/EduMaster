using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>تخفيض مستحق موثق: مبلغ جديد أقل + سبب (D-108) — مفتاح UC-30 الإجرائي</summary>
public sealed record ReduceChargeRequest(int ChargeId, long NewAmountCentimes, string Reason);

public sealed class ReduceChargeHandler
{
    private readonly IChargeRepository _charges;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReduceChargeHandler> _logger;

    public ReduceChargeHandler(IChargeRepository charges, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ReduceChargeHandler> logger)
    {
        _charges = charges;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ReduceChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.NewAmountCentimes < 0)
            return OperationResult.Failure("المبلغ الجديد لا يمكن أن يكون سالباً.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return OperationResult.Failure("سبب التخفيض إلزامي.", ErrorType.Validation);

        try
        {
            var charge = await _charges.GetByIdAsync(request.ChargeId, cancellationToken);
            if (charge is null)
                return OperationResult.Failure("المستحق غير موجود.", ErrorType.NotFound);

            charge.Reduce(request.NewAmountCentimes, request.Reason, _clock.UtcNow, _currentUser.UserAccountId);

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
            _logger.LogError(ex, "Failed to reduce charge {ChargeId} to {NewAmountCentimes}", request.ChargeId, request.NewAmountCentimes);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تخفيض المستحق.", ErrorType.Unexpected);
        }
    }
}