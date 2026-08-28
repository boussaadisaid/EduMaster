using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>إلغاء مستحق موثق بسبب (D-108) — لا حذف للوثائق إطلاقاً (D-109) · يفكّ تخصيصاته في المعاملة نفسها فيعود ماله للزائدة الدائنة (6.6 — ع-1)</summary>
public sealed record CancelChargeRequest(int ChargeId, string Reason);

public sealed class CancelChargeHandler
{
    private readonly IChargeRepository _charges;
    private readonly IPaymentRepository _payments;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelChargeHandler> _logger;

    public CancelChargeHandler(IChargeRepository charges, IPaymentRepository payments, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CancelChargeHandler> logger)
    {
        _charges = charges;
        _payments = payments;
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

            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            charge.Cancel(request.Reason, utcNow, userId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _charges.UpdateAsync(charge, cancellationToken);
            // 6.6-ع-ب2 (ع-1): فكّ تخصيصاته في المعاملة — الجدول فريد الزوج ومشروط الموجب فالإزالة هي المسار المصمَّم ·
            // فيعود ماله للزائدة (Σقبض − Σمخصوص − Σصرف تقرأ الفكّ تلقائياً) · الوثيقتان (الإيصال والمستحق الملغى بسببه) تبقيان — D-109
            await _payments.DeleteAllocationsForChargeAsync(request.ChargeId, cancellationToken);
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