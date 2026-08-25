using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>تعديل سياسة أجر: النوع والقيمة والعلم فقط — الهوية (المستفيد/الفوج) ثابتة (روح D-61)، فلا فحص فرادة هنا لأن النطاق لا يتغير</summary>
public sealed record UpdatePayPolicyRequest(
    int PolicyId, PayPolicyKind Kind, long RateCentimes, decimal? Percentage, bool CountsUnjustifiedAbsent);

public sealed class UpdatePayPolicyHandler
{
    private readonly IPayPolicyRepository _policies;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdatePayPolicyHandler> _logger;

    public UpdatePayPolicyHandler(
        IPayPolicyRepository policies,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<UpdatePayPolicyHandler> logger)
    {
        _policies = policies;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdatePayPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var policy = await _policies.GetByIdAsync(request.PolicyId, cancellationToken);
            if (policy is null)
                return OperationResult.Failure("سياسة الأجر غير موجودة.", ErrorType.NotFound);

            policy.Update(request.Kind, request.RateCentimes, request.Percentage, request.CountsUnjustifiedAbsent,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _policies.UpdateAsync(policy, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update pay policy {PolicyId}", request.PolicyId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل سياسة الأجر.", ErrorType.Unexpected);
        }
    }
}