using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.AcademicYears;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.AcademicYears;

public sealed class GetAcademicYearByIdHandler
{
    private readonly IAcademicYearRepository _years;
    private readonly ILogger<GetAcademicYearByIdHandler> _logger;

    public GetAcademicYearByIdHandler(IAcademicYearRepository years, ILogger<GetAcademicYearByIdHandler> logger)
    {
        _years = years;
        _logger = logger;
    }

    public async Task<OperationResult<AcademicYear?>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var year = await _years.GetByIdAsync(id, cancellationToken);
            return OperationResult<AcademicYear?>.Success(year);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Academic year load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load academic year {AcademicYearId}", id);
            return OperationResult<AcademicYear?>.Failure("حدث خطأ غير متوقع أثناء تحميل السنة الدراسية.", ErrorType.Unexpected);
        }
    }
}