using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>
/// إيصال صرف — استرجاع نقدي للطالب من زائدته الدائنة فقط (D-108، ختام UC-30):
/// حارس «المبلغ ≤ الزائدة المتاحة» إلزامي (لا صرف من الهواء) · سبب إلزامي (مال خارج يُوثَّق) · لا تخصيص أبداً للصرف.
/// يعيد رقم الإيصال للـToast.
/// </summary>
public sealed record RegisterRefundRequest(int StudentId, int TreasuryAccountId, long AmountCentimes, DateOnly PaidOn, string Reason);

public sealed class RegisterRefundHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly ITreasuryAccountRepository _treasuryAccounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterRefundHandler> _logger;

    public RegisterRefundHandler(IPaymentRepository payments, IClock clock, ICurrentUserService currentUser, ITreasuryAccountRepository treasuryAccounts,
        IUnitOfWork unitOfWork, ILogger<RegisterRefundHandler> logger)
    {
        _payments = payments;
        _clock = clock;
        _currentUser = currentUser;
        _treasuryAccounts = treasuryAccounts;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(RegisterRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AmountCentimes <= 0)
            return OperationResult<int>.Failure("مبلغ الاسترجاع يجب أن يكون أكبر من صفر.", ErrorType.Validation);
        if (request.PaidOn > _clock.Today)
            return OperationResult<int>.Failure("تاريخ الصرف لا يمكن أن يكون في المستقبل.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return OperationResult<int>.Failure("سبب الاسترجاع إلزامي — المال الخارج يُوثَّق دائماً.", ErrorType.Validation);

        try
        {
            var treasuryAccount = await _treasuryAccounts.GetByIdAsync(request.TreasuryAccountId, cancellationToken);
            if (treasuryAccount is null)
                return OperationResult<int>.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!treasuryAccount.IsActive)
                return OperationResult<int>.Failure("الحساب المالي معطّل.", ErrorType.BusinessRule);

            // الحارس الأهم: الصرف من الزائدة الدائنة فقط (D-108)
            var availableCredit = await _payments.GetUnallocatedForStudentAsync(request.StudentId, cancellationToken);
            if (request.AmountCentimes > availableCredit)
                return OperationResult<int>.Failure(
                    $"المبلغ يتجاوز الزائدة الدائنة المتاحة لهذا الطالب ({availableCredit / 100m:0.00} دج).", ErrorType.BusinessRule);

            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // D-105: الرقم المتسلسل داخل المعاملة — نفس سلسلة إيصالات القبض
            var receiptNo = await _payments.GetNextReceiptNoAsync(cancellationToken);

            var refund = Domain.Billing.Payment.Create(
                request.StudentId, null, request.TreasuryAccountId, PaymentKind.Refund, request.AmountCentimes, request.PaidOn,
                request.Reason, receiptNo, utcNow, userId);
            await _payments.AddAsync(refund, cancellationToken);

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
            _logger.LogError(ex, "Failed to register refund of {AmountCentimes} centimes for student {StudentId}",
                request.AmountCentimes, request.StudentId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تسجيل الاسترجاع.", ErrorType.Unexpected);
        }
    }
}