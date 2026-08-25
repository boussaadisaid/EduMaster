using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// تفعيل/تعطيل سياسة — الخاملة لا كتابة لها (نمط DeactivatePerson).
/// التفعيل يمرّ بفحص الفرادة على نطاق السياسة نفسها (الفهارس المفلترة تضمن والفحص يوضح — D-22) · التعطيل دائم الجواز (روح D-45).
/// </summary>
public sealed record SetPayPolicyActiveRequest(int PolicyId, bool IsActive);

public sealed class SetPayPolicyActiveHandler
{
    private readonly IPayPolicyRepository _policies;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetPayPolicyActiveHandler> _logger;

    public SetPayPolicyActiveHandler(
        IPayPolicyRepository policies,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<SetPayPolicyActiveHandler> logger)
    {
        _policies = policies;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(SetPayPolicyActiveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var policy = await _policies.GetByIdAsync(request.PolicyId, cancellationToken);
            if (policy is null)
                return OperationResult.Failure("سياسة الأجر غير موجودة.", ErrorType.NotFound);

            if (policy.IsActive == request.IsActive)
                return OperationResult.Success();   // بالحالة المطلوبة أصلاً — لا كتابة بلا معنى

            if (request.IsActive)
            {
                // السياسة معطّلة الآن — أي فعّالة على نفس النطاق هي أخرى فعلاً
                PayPolicy? activeExisting;
                string scopeMessage;
                if (policy.PayeeKind == PayeeKind.Teacher && policy.ClassGroupId is null)
                {
                    activeExisting = await _policies.GetActiveDefaultForTeacherAsync(policy.TeacherId!.Value, cancellationToken);
                    scopeMessage = "توجد سياسة افتراضية فعّالة أخرى لهذا الأستاذ — عطّلها أولاً.";
                }
                else if (policy.PayeeKind == PayeeKind.Teacher)
                {
                    activeExisting = await _policies.GetActiveOverrideAsync(policy.TeacherId!.Value, policy.ClassGroupId!.Value, cancellationToken);
                    scopeMessage = "توجد تجاوز فعّال آخر لهذا الأستاذ على هذا الفوج — عطّله أولاً.";
                }
                else
                {
                    activeExisting = await _policies.GetActiveForEmployeeAsync(policy.EmployeeId!.Value, cancellationToken);
                    scopeMessage = "توجد سياسة فعّالة أخرى لهذا الموظف — عطّلها أولاً.";
                }

                if (activeExisting is not null)
                    return OperationResult.Failure(scopeMessage, ErrorType.Conflict);
            }

            if (request.IsActive)
                policy.Activate(_clock.UtcNow, _currentUser.UserAccountId);
            else
                policy.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _policies.UpdateAsync(policy, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to set pay policy {PolicyId} active={IsActive}", request.PolicyId, request.IsActive);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تغيير حالة سياسة الأجر.", ErrorType.Unexpected);
        }
    }
}