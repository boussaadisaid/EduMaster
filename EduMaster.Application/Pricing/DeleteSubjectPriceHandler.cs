using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Pricing;

/// <summary>D-65: حذف فيزيائي حر — لا أحد يشير إلى الأسعار، والنسخ اللحظية (2.4) تحفظ التاريخ</summary>
public sealed record DeleteSubjectPriceRequest(int SubjectPriceId);

public sealed class DeleteSubjectPriceHandler
{
    private readonly ISubjectPriceRepository _prices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteSubjectPriceHandler> _logger;

    public DeleteSubjectPriceHandler(ISubjectPriceRepository prices, IUnitOfWork unitOfWork,
        ILogger<DeleteSubjectPriceHandler> logger)
    {
        _prices = prices;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(DeleteSubjectPriceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var price = await _prices.GetByIdAsync(request.SubjectPriceId, cancellationToken);
            if (price is null)
                return OperationResult.Failure("السعر غير موجود.", ErrorType.NotFound);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _prices.DeleteAsync(request.SubjectPriceId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to delete subject price {SubjectPriceId}", request.SubjectPriceId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف السعر.", ErrorType.Unexpected);
        }
    }
}