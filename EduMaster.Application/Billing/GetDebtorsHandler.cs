using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>قائمة الديون: من عليهم متبقٍّ > 0 — الأكبر أولاً · بحث مبسّط بالاسم/الهاتف · قراءة بلا معاملة</summary>
public sealed class GetDebtorsHandler
{
    private readonly IChargeRepository _charges;
    private readonly ILogger<GetDebtorsHandler> _logger;

    public GetDebtorsHandler(IChargeRepository charges, ILogger<GetDebtorsHandler> logger)
    {
        _charges = charges;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<DebtorItem>>> ExecuteAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _charges.GetDebtorsAsync(searchTerm, cancellationToken);
            return OperationResult<IReadOnlyList<DebtorItem>>.Success(items.ToList());
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load debtors with term {SearchTerm}", searchTerm);
            return OperationResult<IReadOnlyList<DebtorItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل قائمة الديون.", ErrorType.Unexpected);
        }
    }
}