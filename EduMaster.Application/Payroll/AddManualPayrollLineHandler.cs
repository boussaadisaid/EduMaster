using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// سطر يدوي في مسودة (س-8): مكافأة (+) أو خصم (−) بسبب إلزامي — ينجو من إعادة الحساب الذرّية ويقفل مع الاعتماد.
/// اسم المستفيد يُلتقط لقطةً على السطر (D-52 ممتدة) · المبلغ بالسنتيم — الواجهة تحوّل الدينار عبر MoneyInput (تقبل السالب للخصم).
/// </summary>
public sealed record AddManualPayrollLineRequest(int RunId, PayeeKind PayeeKind, int? TeacherId, int? EmployeeId, long AmountCentimes, string Reason);

public sealed class AddManualPayrollLineHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly ITeacherRepository _teachers;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddManualPayrollLineHandler> _logger;

    public AddManualPayrollLineHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        ITeacherRepository teachers,
        IEmployeeRepository employees,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<AddManualPayrollLineHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _teachers = teachers;
        _employees = employees;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(AddManualPayrollLineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
                return OperationResult<int>.Failure("الكشف غير موجود.", ErrorType.NotFound);
            if (run.IsApproved)
                return OperationResult<int>.Failure("الكشف معتمد ويقفل نهائياً — لا يمكن إضافة سطور.", ErrorType.Conflict);

            // وجود المستفيد + لقطة اسمه (القوائم المسطّحة تحمل FullName)
            string? payeeName;
            if (request.PayeeKind == PayeeKind.Teacher)
            {
                if (request.TeacherId is null or <= 0)
                    return OperationResult<int>.Failure("حدد الأستاذ.", ErrorType.Validation);
                payeeName = (await _teachers.SearchAsync(null, cancellationToken)).FirstOrDefault(t => t.Id == request.TeacherId.Value)?.FullName;
                if (payeeName is null)
                    return OperationResult<int>.Failure("الأستاذ غير موجود.", ErrorType.NotFound);
            }
            else if (request.PayeeKind == PayeeKind.Employee)
            {
                if (request.EmployeeId is null or <= 0)
                    return OperationResult<int>.Failure("حدد الموظف.", ErrorType.Validation);
                payeeName = (await _employees.SearchAsync(null, cancellationToken)).FirstOrDefault(e => e.Id == request.EmployeeId.Value)?.FullName;
                if (payeeName is null)
                    return OperationResult<int>.Failure("الموظف غير موجود.", ErrorType.NotFound);
            }
            else
            {
                return OperationResult<int>.Failure("نوع المستفيد غير صالح.", ErrorType.Validation);
            }

            // حُراس الكيان: مبلغ غير صفري + سبب إلزامي + اتساق المستفيد (مراياها CK في 016)
            var line = PayrollLine.CreateManual(run.Id, request.PayeeKind, request.TeacherId, request.EmployeeId,
                payeeName, request.AmountCentimes, request.Reason, _clock.UtcNow, _currentUser.UserAccountId);

            run.AddLine(line);   // يصون الإجمالي — وحارس «مسودة فقط» داخله

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _lines.AddRangeAsync(new[] { line }, cancellationToken);
            await _runs.UpdateAsync(run, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(line.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while adding manual line to payroll run {RunId} (D-121 trap)", request.RunId);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to add manual line to payroll run {RunId}", request.RunId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة السطر اليدوي.", ErrorType.Unexpected);
        }
    }
}