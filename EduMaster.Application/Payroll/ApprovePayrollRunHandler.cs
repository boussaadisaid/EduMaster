using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// اعتماد كشف (D-116/D-123) — نقطة اللاعودة: يقفل الكشف نهائياً ويختم من اعتمد ومتى.
/// يعيد فحص التداخل مع المعتمدة لحظة الاعتماد (روح D-27) — فقد يكون كشف آخر اعتُمد بين توليد المسودة واعتمادها.
/// الخطأ بعد الاعتماد يُصحَّح بصرف تسوية (5.3) — لا فكّ للقفل.
/// </summary>
public sealed record ApprovePayrollRunRequest(int RunId);

public sealed class ApprovePayrollRunHandler
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollLineRepository _lines;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApprovePayrollRunHandler> _logger;

    public ApprovePayrollRunHandler(
        IPayrollRunRepository runs,
        IPayrollLineRepository lines,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<ApprovePayrollRunHandler> logger)
    {
        _runs = runs;
        _lines = lines;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ApprovePayrollRunRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
                return OperationResult.Failure("الكشف غير موجود.", ErrorType.NotFound);
            if (run.IsApproved)
                return OperationResult.Failure("الكشف معتمد بالفعل.", ErrorType.Conflict);

            run.LoadLines(await _lines.GetByRunAsync(run.Id, cancellationToken));   // حارس «بلا سطور» في الكيان

            // إعادة فحص التداخل لحظة الاعتماد — قد يكون كشف آخر اعتُمد بعد توليد هذه المسودة
            if (await _runs.ExistsApprovedOverlapAsync(run.PeriodStart, run.PeriodEnd, cancellationToken))
                return OperationResult.Failure("تتقاطع فترة هذا الكشف مع كشف اعتُمد بعد توليده — احذف هذه المسودة وأعد توليد كشف خارج تلك الفترة.", ErrorType.Conflict);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            run.Approve(_clock.UtcNow, _currentUser.UserAccountId);
            await _runs.UpdateAsync(run, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while approving payroll run {RunId} (D-121 trap)", request.RunId);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to approve payroll run {RunId}", request.RunId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء اعتماد الكشف.", ErrorType.Unexpected);
        }
    }
}