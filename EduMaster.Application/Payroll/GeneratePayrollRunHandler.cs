using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// توليد مسودة كشف لفترة (D-116/D-123): يمنع التقاطع مع فترة معتمدة (روح D-27) ومع مسودة قائمة (لا تكديس — تُعاد حسابها أو تُحذف)،
/// يحسب بالمحرك النقي عبر خدمة الاحتساب، ويحفظ الكشف وسطوره ذرّياً في معاملة واحدة (D-33).
/// التحذيرات (حصص بلا سياسة مغطية / بلا سياسة أصلاً / بلقطة فارغة) تُعاد ضمن النتيجة للعرض — لا تُسقَط بصمت.
/// </summary>
public sealed record GeneratePayrollRunRequest(DateOnly From, DateOnly To);

public sealed record PayrollRunGenerationResult(int RunId, int LinesCount, long TotalCentimes, IReadOnlyList<string> Warnings);

public sealed class GeneratePayrollRunHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly PayrollComputationService _composer;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GeneratePayrollRunHandler> _logger;

    public GeneratePayrollRunHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        PayrollComputationService composer,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<GeneratePayrollRunHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _composer = composer;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<PayrollRunGenerationResult>> ExecuteAsync(GeneratePayrollRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // حارس «لا ازدواج احتساب» — الفترة المعتمدة مقفلة نهائياً (س-3)
            if (await _runs.ExistsApprovedOverlapAsync(request.From, request.To, cancellationToken))
                return OperationResult<PayrollRunGenerationResult>.Failure(
                    "تتقاطع هذه الفترة مع كشف معتمد — الفترات المعتمدة لا يُعاد احتسابها.", ErrorType.Conflict);

            // حارس «لا تكديس مسودات» — مسودة الفترة تُعاد حسابها أو تُحذف، لا تُكرَّر
            if (await _runs.ExistsDraftOverlapAsync(request.From, request.To, cancellationToken))
                return OperationResult<PayrollRunGenerationResult>.Failure(
                    "توجد مسودة قائمة تتقاطع مع هذه الفترة — أعد حسابها (🔁) أو احذفها (🗑) بدل تكديس المسودات.", ErrorType.Conflict);

            var outcome = await _composer.ComputeAsync(request.From, request.To, cancellationToken);

            var run = PayrollRun.CreateDraft(request.From, request.To, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _runs.AddAsync(run, cancellationToken);   // المعرف يُعيَّن هنا — السطور تتبعه

            foreach (var line in outcome.Lines)
                run.AddLine(PayrollLine.CreateComputed(run.Id,
                    line.Spec.PayeeKind, line.Spec.TeacherId, line.Spec.EmployeeId, line.PayeeName,
                    line.Spec.PolicyId, line.Spec.Kind, line.Spec.RateCentimes, line.Spec.Percentage, line.Spec.CountsUnjustifiedAbsent,
                    line.Spec.Quantity, line.Spec.Details, line.Spec.AmountCentimes,
                    _clock.UtcNow, _currentUser.UserAccountId));

            await _lines.AddRangeAsync(run.Lines, cancellationToken);
            await _runs.UpdateAsync(run, cancellationToken);   // يخزّن الإجمالي المصان من AddLine
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<PayrollRunGenerationResult>.Success(
                new PayrollRunGenerationResult(run.Id, run.Lines.Count, run.TotalCentimes, outcome.Warnings));
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while generating payroll run {From}..{To} (D-121 trap)", request.From, request.To);
            return OperationResult<PayrollRunGenerationResult>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to generate payroll run {From}..{To}", request.From, request.To);
            return OperationResult<PayrollRunGenerationResult>.Failure("حدث خطأ غير متوقع أثناء توليد الكشف.", ErrorType.Unexpected);
        }
    }
}