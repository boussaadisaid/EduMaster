using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>أيام عمل موظف بنطاق اختياري — للعرض الآن ولاحتساب «باليوم» في 5.2 · الإلغاء ليس خطأً (D-64)</summary>
public sealed record GetWorkLogRequest(int EmployeeId, DateOnly? From, DateOnly? To);

public sealed class GetWorkLogHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeWorkLogRepository _workLog;
    private readonly ILogger<GetWorkLogHandler> _logger;

    public GetWorkLogHandler(
        IEmployeeRepository employees,
        IEmployeeWorkLogRepository workLog,
        ILogger<GetWorkLogHandler> logger)
    {
        _employees = employees;
        _workLog = workLog;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<WorkLogItem>>> ExecuteAsync(
        GetWorkLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
                return OperationResult<IReadOnlyList<WorkLogItem>>.Failure("الموظف غير موجود.", ErrorType.NotFound);

            if (request.From is not null && request.To is not null && request.From > request.To)
                return OperationResult<IReadOnlyList<WorkLogItem>>.Failure("بداية الفترة بعد نهايتها.", ErrorType.Validation);

            var items = await _workLog.GetForEmployeeAsync(request.EmployeeId, request.From, request.To, cancellationToken);
            return OperationResult<IReadOnlyList<WorkLogItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: الإلغاء ليس خطأً
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient قد يلفّ الإلغاء داخل SqlException (D-64)
            throw new OperationCanceledException("Work log read cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get work log for employee {EmployeeId}", request.EmployeeId);
            return OperationResult<IReadOnlyList<WorkLogItem>>.Failure(
                "حدث خطأ غير متوقع أثناء جلب سجل العمل.", ErrorType.Unexpected);
        }
    }
}