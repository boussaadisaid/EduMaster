using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.ClassGroups;

public sealed class GetClassGroupsHandler
{
    private readonly IClassGroupRepository _classGroups;
    private readonly ILogger<GetClassGroupsHandler> _logger;

    public GetClassGroupsHandler(IClassGroupRepository classGroups, ILogger<GetClassGroupsHandler> logger)
    {
        _classGroups = classGroups;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ClassGroupListItem>>> ExecuteAsync(
        int? academicYearId, string? searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            // D-32: المصطلح يُطبَّع بنفس دالة الكتابة
            var normalized = string.IsNullOrWhiteSpace(searchTerm) ? null : ArabicTextNormalizer.Normalize(searchTerm);

            var items = (await _classGroups.SearchAsync(academicYearId, normalized, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ClassGroupListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // إلغاء طلب سابق أثناء الكتابة — ليس خطأً، يُعالجه المتصل بصمت
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient قد يلفّ إلغاء الأمر الجاري داخل SqlException («Operation cancelled by user») — الإلغاء ليس خطأً
            throw new OperationCanceledException("Class groups search cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search class groups for year {AcademicYearId} with term {SearchTerm}", academicYearId, searchTerm);
            return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل الأفواج.", ErrorType.Unexpected);
        }
    }
}