using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>
/// تسجيل قبض (D-104…D-107): إيصال متسلسل + تخصيصات في معاملة واحدة.
/// الحُراس: مبلغ > 0 · تاريخ لا يستقبل المستقبل · لا مستحق مكرراً · Σتخصيص ≤ المبلغ · كل تخصيص على مستحق فعّال
/// لنفس الطالب وضمن متبقّيه · الفائض زائدة دائنة (مسموح ومرئي — لا سحري).
/// يعيد رقم الإيصال للـToast.
/// </summary>
public sealed record RegisterPaymentRequest(
    int StudentId,
    int? PaidByPersonId,
    int TreasuryAccountId,
    long AmountCentimes,
    DateOnly PaidOn,
    string? Note,
    IReadOnlyList<PaymentAllocationInput>? Allocations);

public sealed class RegisterPaymentHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IChargeRepository _charges;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly ITreasuryAccountRepository _treasuryAccounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreditConsumptionService _creditConsumption;
    private readonly ILogger<RegisterPaymentHandler> _logger;

    public RegisterPaymentHandler(IPaymentRepository payments, IChargeRepository charges,
        IClock clock, ICurrentUserService currentUser, ITreasuryAccountRepository treasuryAccounts, IUnitOfWork unitOfWork,
        CreditConsumptionService creditConsumption, ILogger<RegisterPaymentHandler> logger)
    {
        _payments = payments;
        _charges = charges;
        _clock = clock;
        _currentUser = currentUser;
        _treasuryAccounts = treasuryAccounts;
        _unitOfWork = unitOfWork;
        _creditConsumption = creditConsumption;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(RegisterPaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AmountCentimes <= 0)
            return OperationResult<int>.Failure("مبلغ القبض يجب أن يكون أكبر من صفر.", ErrorType.Validation);
        if (request.PaidOn > _clock.Today)
            return OperationResult<int>.Failure("تاريخ القبض لا يمكن أن يكون في المستقبل.", ErrorType.Validation);

        var allocations = (request.Allocations ?? Array.Empty<PaymentAllocationInput>())
            .Where(a => a.AmountCentimes > 0)
            .ToList();

        if (allocations.Any(a => a.AmountCentimes <= 0))
            return OperationResult<int>.Failure("مبلغ التخصيص يجب أن يكون أكبر من صفر.", ErrorType.Validation);
        if (allocations.GroupBy(a => a.ChargeId).Any(g => g.Count() > 1))
            return OperationResult<int>.Failure("نفس المستحق مكرر في التخصيص — ادمج سطره في مبلغ واحد.", ErrorType.Validation);

        var allocatedTotal = allocations.Sum(a => a.AmountCentimes);
        if (allocatedTotal > request.AmountCentimes)
            return OperationResult<int>.Failure(
                $"مجموع التخصيصات ({allocatedTotal / 100m:0.00} دج) يتجاوز المبلغ المقبوض ({request.AmountCentimes / 100m:0.00} دج).", ErrorType.Validation);

        try
        {
            var treasuryAccount = await _treasuryAccounts.GetByIdAsync(request.TreasuryAccountId, cancellationToken);
            if (treasuryAccount is null)
                return OperationResult<int>.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!treasuryAccount.IsActive)
                return OperationResult<int>.Failure("الحساب المالي معطّل.", ErrorType.BusinessRule);

            // المفتوحة مرة واحدة — خريطة المتبقّي للفحص
            var openCharges = await _charges.GetOpenForStudentAsync(request.StudentId, cancellationToken);
            var openById = openCharges.ToDictionary(o => o.Id);

            foreach (var input in allocations)
            {
                var charge = await _charges.GetByIdAsync(input.ChargeId, cancellationToken);
                if (charge is null)
                    return OperationResult<int>.Failure("أحد المستحقات المختارة غير موجود.", ErrorType.Validation);
                if (charge.StudentId != request.StudentId)
                    return OperationResult<int>.Failure("أحد المستحقات المختارة لا يتبع هذا الطالب.", ErrorType.Validation);
                if (charge.Status != ChargeStatus.Active)
                    return OperationResult<int>.Failure("أحد المستحقات المختارة مسوّى — لا يقبل تخصيصاً. حدّث السياق وأعد المحاولة.", ErrorType.BusinessRule);

                if (!openById.TryGetValue(input.ChargeId, out var open) || input.AmountCentimes > open.RemainingCentimes)
                    return OperationResult<int>.Failure(
                        $"تخصيص يتجاوز المتبقي من مستحق ({open?.RemainingCentimes / 100m:0.00} دج) — حدّث السياق وأعد المحاولة.", ErrorType.Validation);
            }

            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // D-105: الرقم المتسلسل داخل المعاملة — والفهرس الفريد يحرسه
            var receiptNo = await _payments.GetNextReceiptNoAsync(cancellationToken);

            var payment = Domain.Billing.Payment.Create(
                request.StudentId, request.PaidByPersonId, request.TreasuryAccountId, PaymentKind.Receipt, request.AmountCentimes, request.PaidOn,
                request.Note, receiptNo, utcNow, userId);
            await _payments.AddAsync(payment, cancellationToken);

            foreach (var input in allocations)
            {
                var allocation = Domain.Billing.PaymentAllocation.Create(payment.Id, input.ChargeId, input.AmountCentimes, utcNow, userId);
                await _payments.AddAllocationAsync(allocation, cancellationToken);
            }

            // 6.6 — ز-1: فائض هذا الإيصال (والزائدة القائمة) يسيل فوراً على المستحقات المفتوحة الأخرى — سداد وعد D-107 داخل المعاملة ذاتها
            await _creditConsumption.ConsumeForStudentAsync(request.StudentId, utcNow, userId, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(receiptNo);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to register payment of {AmountCentimes} centimes for student {StudentId}",
                request.AmountCentimes, request.StudentId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تسجيل القبض.", ErrorType.Unexpected);
        }
    }
}