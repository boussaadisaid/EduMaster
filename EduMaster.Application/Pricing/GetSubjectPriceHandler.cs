using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Pricing;

/// <summary>السعر المقترح من جدول الأسعار (سنة/مستوى/مادة) — null داخل النجاح = لا سعر في الجدول (إدخال يدوي إلزامي — D-77)</summary>
public sealed class GetSubjectPriceHandler
{
    private readonly ISubjectPriceRepository _prices;
    private readonly ILogger<GetSubjectPriceHandler> _logger;

    public GetSubjectPriceHandler(ISubjectPriceRepository prices, ILogger<GetSubjectPriceHandler> logger)
    {
        _prices = prices;
        _logger = logger;
    }

    public async Task<OperationResult<long?>> ExecuteAsync(
        int academicYearId, int levelId, int subjectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var price = await _prices.TryGetPriceAsync(academicYearId, levelId, subjectId, cancellationToken);
            return OperationResult<long?>.Success(price);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Subject price suggestion load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load price suggestion for year {AcademicYearId}, level {LevelId}, subject {SubjectId}",
                academicYearId, levelId, subjectId);
            return OperationResult<long?>.Failure("حدث خطأ غير متوقع أثناء جلب السعر المقترح.", ErrorType.Unexpected);
        }
    }
}