using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Employees;

/// <summary>بحث الموظفين الحي — المصطلح يُطبَّع بدالة الكتابة نفسها (D-32) · الإلغاء ليس خطأً (D-64)</summary>
public sealed class GetEmployeesHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly ILogger<GetEmployeesHandler> _logger;

    public GetEmployeesHandler(IEmployeeRepository employees, ILogger<GetEmployeesHandler> logger)
    {
        _employees = employees;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<EmployeeListItem>>> ExecuteAsync(
        string? searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            // D-32: المصطلح يُطبَّع بنفس دالة الكتابة
            var normalized = string.IsNullOrWhiteSpace(searchTerm) ? null : ArabicTextNormalizer.Normalize(searchTerm);

            var items = (await _employees.SearchAsync(normalized, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<EmployeeListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // إلغاء طلب سابق أثناء الكتابة — ليس خطأً، يُعالجه المتصل بصمت (D-64)
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient قد يلفّ إلغاء الأمر الجاري داخل SqlException («Operation cancelled by user») — الإلغاء ليس خطأً (D-64)
            throw new OperationCanceledException("Employees search cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search employees with term {SearchTerm}", searchTerm);
            return OperationResult<IReadOnlyList<EmployeeListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء البحث عن الموظفين.", ErrorType.Unexpected);
        }
    }
}