using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// إعادة حساب مسودة ذرّياً (روح D-101): تسقط السطور المحسوبة (قاعدةً وكياناً) وتُعيد توليدها من المصدر الحيّ —
/// السطور اليدوية (مكافأة/خصم — س-8) تنجو. المعتمد لا يُعاد حسابه أبداً — يقفل نهائياً.
/// </summary>
public sealed record RegeneratePayrollRunRequest(int RunId);

public sealed class RegeneratePayrollRunHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly PayrollComputationService _composer;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegeneratePayrollRunHandler> _logger;

    public RegeneratePayrollRunHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        PayrollComputationService composer,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<RegeneratePayrollRunHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _composer = composer;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<PayrollRunGenerationResult>> ExecuteAsync(RegeneratePayrollRunRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
                return OperationResult<PayrollRunGenerationResult>.Failure("الكشف غير موجود.", ErrorType.NotFound);
            if (run.IsApproved)
                return OperationResult<PayrollRunGenerationResult>.Failure("الكشف معتمد ويقفل نهائياً — لا يُعاد حسابه.", ErrorType.Conflict);

            run.LoadLines(await _lines.GetByRunAsync(run.Id, cancellationToken));   // اليدوية تبقى داخل الكيان

            var outcome = await _composer.ComputeAsync(run.PeriodStart, run.PeriodEnd, cancellationToken);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            run.ClearComputedLines();
            await _lines.DeleteComputedForRunAsync(run.Id, cancellationToken);

            foreach (var line in outcome.Lines)
                run.AddLine(PayrollLine.CreateComputed(run.Id,
                    line.Spec.PayeeKind, line.Spec.TeacherId, line.Spec.EmployeeId, line.PayeeName,
                    line.Spec.PolicyId, line.Spec.Kind, line.Spec.RateCentimes, line.Spec.Percentage, line.Spec.CountsUnjustifiedAbsent,
                    line.Spec.Quantity, line.Spec.Details, line.Spec.AmountCentimes,
                    _clock.UtcNow, _currentUser.UserAccountId));

            // الجديد فقط يُدرج — اليدوية الناجية مُدرجة أصلاً (معرّفها > 0)
            await _lines.AddRangeAsync(run.Lines.Where(l => l.Id == 0).ToList(), cancellationToken);
            await _runs.UpdateAsync(run, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<PayrollRunGenerationResult>.Success(
                new PayrollRunGenerationResult(run.Id, run.Lines.Count, run.TotalCentimes, outcome.Warnings));
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while regenerating payroll run {RunId} (D-121 trap)", request.RunId);
            return OperationResult<PayrollRunGenerationResult>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to regenerate payroll run {RunId}", request.RunId);
            return OperationResult<PayrollRunGenerationResult>.Failure("حدث خطأ غير متوقع أثناء إعادة حساب الكشف.", ErrorType.Unexpected);
        }
    }
}