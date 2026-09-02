using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>سجل إيصالات مستفيد (لديالوغ الصرف — 5.3) — الأحدث أولاً · قيود التصحيح السالبة تظهر بجانب مصروفاته (س-5).</summary>
public sealed record GetPayeePayoutsRequest(PayeeKind PayeeKind, int PayeeId);

public sealed class GetPayeePayoutsHandler
{
    private readonly IPayoutRepository _payouts;
    private readonly ILogger<GetPayeePayoutsHandler> _logger;

    public GetPayeePayoutsHandler(IPayoutRepository payouts, ILogger<GetPayeePayoutsHandler> logger)
    {
        _payouts = payouts;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<PayoutItem>>> ExecuteAsync(GetPayeePayoutsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payouts = await _payouts.GetForPayeeAsync(request.PayeeKind, request.PayeeId, cancellationToken);

            var items = payouts
                .Select(p => new PayoutItem(p.Id, p.ReceiptNo, p.AmountCentimes, p.Note, p.PayrollRunId, p.TreasuryAccountId, p.PayoutDate, p.CreatedAtUtc))
                .ToList();

            return OperationResult<IReadOnlyList<PayoutItem>>.Success(items);
        }
        catch (OperationCanceledException) { throw; }   // D-64: الإلغاء ليس خطأً
        catch (Exception ex) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("أُلغي تحميل الإيصالات.", ex, cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load payouts for {PayeeKind} {PayeeId}", request.PayeeKind, request.PayeeId);
            return OperationResult<IReadOnlyList<PayoutItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل الإيصالات.", ErrorType.Unexpected);
        }
    }
}