using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Pricing;

public sealed class GetSubjectPricesHandler
{
    private readonly ISubjectPriceRepository _prices;
    private readonly ILogger<GetSubjectPricesHandler> _logger;

    public GetSubjectPricesHandler(ISubjectPriceRepository prices, ILogger<GetSubjectPricesHandler> logger)
    {
        _prices = prices;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<SubjectPriceListItem>>> ExecuteAsync(
        int? academicYearId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _prices.GetByYearAsync(academicYearId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<SubjectPriceListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: الإلغاء ليس خطأ
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient قد يلفّ إلغاء الأمر الجاري داخل SqlException (D-64)
            throw new OperationCanceledException("Subject prices load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load subject prices for year {AcademicYearId}", academicYearId);
            return OperationResult<IReadOnlyList<SubjectPriceListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل الأسعار.", ErrorType.Unexpected);
        }
    }
}