using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>جلب سياسات الأجر بفلتر اختياري (نوع المستفيد + معرّفه) — الإلغاء ليس خطأً (D-64)</summary>
public sealed record GetPayPoliciesRequest(PayeeKind? PayeeKind, int? PayeeId);

public sealed class GetPayPoliciesHandler
{
    private readonly IPayPolicyRepository _policies;
    private readonly ILogger<GetPayPoliciesHandler> _logger;

    public GetPayPoliciesHandler(IPayPolicyRepository policies, ILogger<GetPayPoliciesHandler> logger)
    {
        _policies = policies;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<PayPolicyItem>>> ExecuteAsync(
        GetPayPoliciesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var items = await _policies.ListAsync(request.PayeeKind, request.PayeeId, cancellationToken);
            return OperationResult<IReadOnlyList<PayPolicyItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: الإلغاء ليس خطأً
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient قد يلفّ الإلغاء داخل SqlException (D-64)
            throw new OperationCanceledException("Pay policies read cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list pay policies for {PayeeKind} {PayeeId}", request.PayeeKind, request.PayeeId);
            return OperationResult<IReadOnlyList<PayPolicyItem>>.Failure(
                "حدث خطأ غير متوقع أثناء جلب سياسات الأجر.", ErrorType.Unexpected);
        }
    }
}