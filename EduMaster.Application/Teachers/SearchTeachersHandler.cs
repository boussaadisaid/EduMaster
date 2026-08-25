using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Teachers;

public sealed class SearchTeachersHandler
{
    private readonly ITeacherRepository _teachers;
    private readonly ILogger<SearchTeachersHandler> _logger;

    public SearchTeachersHandler(ITeacherRepository teachers, ILogger<SearchTeachersHandler> logger)
    {
        _teachers = teachers;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<TeacherListItem>>> ExecuteAsync(
        string? searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            // D-32: المصطلح يُطبَّع بنفس دالة الكتابة
            var normalized = string.IsNullOrWhiteSpace(searchTerm) ? null : ArabicTextNormalizer.Normalize(searchTerm);

            var items = (await _teachers.SearchAsync(normalized, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<TeacherListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: الإلغاء ليس خطأً
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // D-64: SqlClient قد يلفّ الإلغاء داخل SqlException
            throw new OperationCanceledException("Teachers search cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search teachers with term {SearchTerm}", searchTerm);
            return OperationResult<IReadOnlyList<TeacherListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء البحث عن الأساتذة.", ErrorType.Unexpected);
        }
    }
}