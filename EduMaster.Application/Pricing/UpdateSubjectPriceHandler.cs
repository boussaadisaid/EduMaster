using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Pricing;

/// <summary>الهوية (سنة/مستوى/مادة) ثابتة — التعديل يمس السعر فقط (D-65)</summary>
public sealed record UpdateSubjectPriceRequest(int SubjectPriceId, long UnitPriceCentimes);

public sealed class UpdateSubjectPriceHandler
{
    private readonly ISubjectPriceRepository _prices;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSubjectPriceHandler> _logger;

    public UpdateSubjectPriceHandler(ISubjectPriceRepository prices, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateSubjectPriceHandler> logger)
    {
        _prices = prices;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateSubjectPriceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UnitPriceCentimes < 0)
            return OperationResult.Failure("سعر الحصة لا يمكن أن يكون سالباً.", ErrorType.Validation);

        try
        {
            var price = await _prices.GetByIdAsync(request.SubjectPriceId, cancellationToken);
            if (price is null)
                return OperationResult.Failure("السعر غير موجود.", ErrorType.NotFound);

            price.Update(request.UnitPriceCentimes, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _prices.UpdateAsync(price, cancellationToken);
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
            _logger.LogError(ex, "Failed to update subject price {SubjectPriceId}", request.SubjectPriceId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل السعر.", ErrorType.Unexpected);
        }
    }
}