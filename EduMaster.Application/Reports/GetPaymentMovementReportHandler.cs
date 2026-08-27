using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Reports;

/// <summary>حركة القبض لفترة (6.1 — D-127): سجل المدفوعات القائم + إجماليات مشتقة — قراءة خالصة بلا معاملة، ترمي الإلغاء (D-64)</summary>
public sealed class GetPaymentMovementReportHandler
{
    private readonly IPaymentRepository _payments;
    private readonly ILogger<GetPaymentMovementReportHandler> _logger;

    public GetPaymentMovementReportHandler(IPaymentRepository payments, ILogger<GetPaymentMovementReportHandler> logger)
    {
        _payments = payments;
        _logger = logger;
    }

    public async Task<OperationResult<PaymentMovementReportItem>> ExecuteAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return OperationResult<PaymentMovementReportItem>.Failure("تاريخ «من» لا يمكن أن يكون بعد «إلى».", ErrorType.Validation);

        try
        {
            var rows = await _payments.GetForPeriodAsync(from, to, cancellationToken);
            return OperationResult<PaymentMovementReportItem>.Success(new PaymentMovementReportItem(from, to, rows.ToList()));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build payment movement report from {From} to {To}", from, to);
            return OperationResult<PaymentMovementReportItem>.Failure("حدث خطأ غير متوقع أثناء إعداد تقرير حركة القبض.", ErrorType.Unexpected);
        }
    }
}