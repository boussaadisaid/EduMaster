using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.AcademicYears;

/// <summary>سطر عرض في قائمة السنوات — DTO مسطّح بلا VOs مخصص للواجهة</summary>
public sealed record AcademicYearListItem(
    int Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent, bool IsActive);

public sealed class GetAllAcademicYearsHandler
{
    private readonly IAcademicYearRepository _years;
    private readonly ILogger<GetAllAcademicYearsHandler> _logger;

    public GetAllAcademicYearsHandler(IAcademicYearRepository years, ILogger<GetAllAcademicYearsHandler> logger)
    {
        _years = years;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<AcademicYearListItem>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var years = await _years.GetAllAsync(cancellationToken);

            var items = years
                .Select(y => new AcademicYearListItem(y.Id, y.Name.Value, y.StartDate, y.EndDate, y.IsCurrent, y.IsActive))
                .ToList();

            return OperationResult<IReadOnlyList<AcademicYearListItem>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load academic years.");
            return OperationResult<IReadOnlyList<AcademicYearListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل قائمة السنوات الدراسية.", ErrorType.Unexpected);
        }
    }
}
