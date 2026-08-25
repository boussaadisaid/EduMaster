using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// تسجيل يوم عمل لموظف (D-115 — أساس الأجر اليومي غير المنتظم).
/// حارس «لا تاريخ مستقبل» يعيش هنا — الكيان لا يقرأ الساعة (D-20) · الفرادة (الموظف، اليوم) قاعدةً + فحص ودّي (D-22).
/// </summary>
public sealed record AddWorkLogDayRequest(int EmployeeId, DateOnly WorkDate, string? Note);

public sealed class AddWorkLogDayHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeWorkLogRepository _workLog;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddWorkLogDayHandler> _logger;

    public AddWorkLogDayHandler(
        IEmployeeRepository employees,
        IEmployeeWorkLogRepository workLog,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<AddWorkLogDayHandler> logger)
    {
        _employees = employees;
        _workLog = workLog;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(AddWorkLogDayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
                return OperationResult<int>.Failure("الموظف غير موجود.", ErrorType.NotFound);

            if (request.WorkDate > _clock.Today)
                return OperationResult<int>.Failure("لا يمكن تسجيل يوم عمل في المستقبل.", ErrorType.Validation);

            // الفرادة تضمنها القاعدة (UX_EmployeeWorkLog_Employee_Date) — والفحص يعطي الرسالة النظيفة
            var sameDay = await _workLog.GetForEmployeeAsync(request.EmployeeId, request.WorkDate, request.WorkDate, cancellationToken);
            if (sameDay.Count > 0)
                return OperationResult<int>.Failure("هذا اليوم مسجَّل لهذا الموظف — احذفه أولاً إن أردت تصحيحه.", ErrorType.Conflict);

            var entry = WorkLogEntry.Create(request.EmployeeId, request.WorkDate, request.Note,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _workLog.AddAsync(entry, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(entry.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while adding work log day {WorkDate} for employee {EmployeeId} — temporary diagnostics (B-3 incident)", request.WorkDate, request.EmployeeId);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to add work log day {WorkDate} for employee {EmployeeId}", request.WorkDate, request.EmployeeId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تسجيل يوم العمل.", ErrorType.Unexpected);
        }
    }
}