using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Students;

public sealed class SearchStudentsHandler
{
    private readonly IStudentRepository _students;
    private readonly ILogger<SearchStudentsHandler> _logger;

    public SearchStudentsHandler(IStudentRepository students, ILogger<SearchStudentsHandler> logger)
    {
        _students = students;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<StudentListItem>>> ExecuteAsync(
        string? searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            // ح-3/D-32: المصطلح يُطبَّع بنفس دالة الكتابة
            var normalized = string.IsNullOrWhiteSpace(searchTerm) ? null : ArabicTextNormalizer.Normalize(searchTerm);

            var items = (await _students.SearchAsync(normalized, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<StudentListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: الإلغاء ليس خطأً
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // D-64: SqlClient قد يلفّ الإلغاء داخل SqlException
            throw new OperationCanceledException("Students search cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search students with term {SearchTerm}", searchTerm);
            return OperationResult<IReadOnlyList<StudentListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء البحث عن الطلاب.", ErrorType.Unexpected);
        }
    }
}