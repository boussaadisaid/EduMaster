using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>سجل المدفوعات لفترة (قبض + صرف) — الأحدث أولاً · قراءة بلا معاملة</summary>
public sealed class GetPaymentsLogHandler
{
    private readonly IPaymentRepository _payments;
    private readonly ILogger<GetPaymentsLogHandler> _logger;

    public GetPaymentsLogHandler(IPaymentRepository payments, ILogger<GetPaymentsLogHandler> logger)
    {
        _payments = payments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<PaymentListItem>>> ExecuteAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return OperationResult<IReadOnlyList<PaymentListItem>>.Failure("تاريخ «من» لا يمكن أن يكون بعد «إلى».", ErrorType.Validation);

        try
        {
            var items = await _payments.GetForPeriodAsync(from, to, cancellationToken);
            return OperationResult<IReadOnlyList<PaymentListItem>>.Success(items.ToList());
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load payments log from {From} to {To}", from, to);
            return OperationResult<IReadOnlyList<PaymentListItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل سجل المدفوعات.", ErrorType.Unexpected);
        }
    }
}