using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Employees;

/// <summary>إزالة ملف موظف منطقياً (D-39) — للأخطاء فقط: ملف عليه سياسات/أيام عمل يبقى للأرشيف (بروح D-109)</summary>
public sealed record SoftDeleteEmployeeRequest(int EmployeeId);

public sealed class SoftDeleteEmployeeHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SoftDeleteEmployeeHandler> _logger;

    public SoftDeleteEmployeeHandler(
        IEmployeeRepository employees,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<SoftDeleteEmployeeHandler> logger)
    {
        _employees = employees;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(SoftDeleteEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
                return OperationResult.Failure("ملف الموظف غير موجود.", ErrorType.NotFound);

            // الإزالة للأخطاء فقط — ملف عليه بيانات تشغيلية يبقى للأرشيف
            if (await _employees.HasOperationalDataAsync(request.EmployeeId, cancellationToken))
                return OperationResult.Failure("لا يمكن إزالة ملف عليه بيانات تشغيلية (سياسات أجر/أيام عمل…). يبقى للأرشيف — ويمكنك تعطيل الشخص بدلاً من ذلك.", ErrorType.BusinessRule);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _employees.SoftDeleteAsync(request.EmployeeId, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Admin {AdminUserId} soft-deleted employee file {EmployeeId}", _currentUser.UserAccountId, request.EmployeeId);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to soft-delete employee {EmployeeId}", request.EmployeeId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء إزالة ملف الموظف.", ErrorType.Unexpected);
        }
    }
}