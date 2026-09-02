using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>
/// عكس إيصال قبض خاطئ (6.6-ع-4 — وُلدت من سؤال المستخدم «كيف أصحّح إدخالاً خاطئاً؟»): بلا حذف إطلاقاً (D-109) —
/// في معاملة واحدة: إيصال صرف معاكس بنفس المبلغ (بوسم «↩ عكس الإيصال #…» + السبب الموثّق) + فكّ تخصيصات الإيصال الأصلي
/// (الجدول فريد الزوج ومشروط الموجب — الإزالة هي فكّ التخصيص المصمَّمة، ع-ب2) — فيُصفَّر أثره النقدي وتعود مستحقاته مفتوحةً بمتبقيها الصحيح، والتاريخ كامل مرئي.
/// بلا حارس زائدة: العكس متوازن ذاتياً بالبناء (الصرف = الأصل وتخصيصاته تُفكّ معه) — لا يُستعمل إلا من هنا.
/// يعيد رقم إيصال العكس للـToast.
/// </summary>
public sealed record ReverseReceiptRequest(int PaymentId, string Reason);

public sealed class ReverseReceiptHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReverseReceiptHandler> _logger;

    public ReverseReceiptHandler(IPaymentRepository payments, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ReverseReceiptHandler> logger)
    {
        _payments = payments;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(ReverseReceiptRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
            return OperationResult<int>.Failure("سبب العكس إلزامي — الإيصال المعكوس يُوثَّق دائماً.", ErrorType.Validation);

        try
        {
            var info = await _payments.GetReceiptReversalInfoAsync(request.PaymentId, cancellationToken);
            if (info is null)
                return OperationResult<int>.Failure("الإيصال غير موجود.", ErrorType.NotFound);
            if (info.Kind != (byte)PaymentKind.Receipt)
                return OperationResult<int>.Failure("العكس لإيصالات القبض فقط — الاسترجاع يُعالج بقبض جديد مصحّح.", ErrorType.BusinessRule);
            if (info.AlreadyReversed)
                return OperationResult<int>.Failure($"هذا الإيصال عُكس من قبل (#{info.ReceiptNo:000000}) — لا يُعكَس مرتين.", ErrorType.Conflict);

            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // D-105: الرقم المتسلسل داخل المعاملة — نفس سلسلة الإيصالات
            var receiptNo = await _payments.GetNextReceiptNoAsync(cancellationToken);

            var reversal = Domain.Billing.Payment.Create(
                info.StudentId, null, info.TreasuryAccountId, PaymentKind.Refund, info.AmountCentimes, _clock.Today,
                $"↩ عكس الإيصال #{info.ReceiptNo:000000} — {request.Reason.Trim()}", receiptNo, utcNow, userId);
            await _payments.AddAsync(reversal, cancellationToken);

            // فكّ تخصيصات الإيصال الأصلي — فينقص مخصوصه وترتفع حرّيته وتعود المستحقات مفتوحة (الإزالة مصمَّمة — ع-ب2)
            await _payments.DeleteAllocationsForPaymentAsync(request.PaymentId, cancellationToken);

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
            _logger.LogError(ex, "Failed to reverse receipt payment {PaymentId}", request.PaymentId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء عكس الإيصال.", ErrorType.Unexpected);
        }
    }
}
