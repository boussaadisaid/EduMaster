using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// إنشاء سياسة أجر موحّدة (D-113/D-114): افتراضية على أستاذ · أو تجاوز لفوج · أو سياسة موظف.
/// الفرادة تضمنها الفهارس المفلترة الثلاثة (015) والفحص الودّي يعطي الرسالة النظيفة (D-22) · اتساق النوع/القيمة/العلم يحرسه الكيان.
/// </summary>
public sealed record CreatePayPolicyRequest(
    PayeeKind PayeeKind,
    int? TeacherId, int? EmployeeId, int? ClassGroupId,
    PayPolicyKind Kind, long RateCentimes, decimal? Percentage,
    bool CountsUnjustifiedAbsent);

public sealed class CreatePayPolicyHandler
{
    private readonly IPayPolicyRepository _policies;
    private readonly ITeacherRepository _teachers;
    private readonly IEmployeeRepository _employees;
    private readonly IClassGroupRepository _classGroups;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreatePayPolicyHandler> _logger;

    public CreatePayPolicyHandler(
        IPayPolicyRepository policies,
        ITeacherRepository teachers,
        IEmployeeRepository employees,
        IClassGroupRepository classGroups,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreatePayPolicyHandler> logger)
    {
        _policies = policies;
        _teachers = teachers;
        _employees = employees;
        _classGroups = classGroups;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreatePayPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.PayeeKind))
            return OperationResult<int>.Failure("نوع المستفيد غير صالح.", ErrorType.Validation);

        try
        {
            // وجود المستفيد (اتساق النوع مع المستفيد يحرسه الكيان — هنا الوجود فقط)
            if (request.PayeeKind == PayeeKind.Teacher)
            {
                if (request.TeacherId is null or <= 0)
                    return OperationResult<int>.Failure("حدد الأستاذ.", ErrorType.Validation);
                if (await _teachers.GetByIdAsync(request.TeacherId.Value, cancellationToken) is null)
                    return OperationResult<int>.Failure("الأستاذ غير موجود.", ErrorType.NotFound);
            }
            else
            {
                if (request.EmployeeId is null or <= 0)
                    return OperationResult<int>.Failure("حدد الموظف.", ErrorType.Validation);
                if (await _employees.GetByIdAsync(request.EmployeeId.Value, cancellationToken) is null)
                    return OperationResult<int>.Failure("الموظف غير موجود.", ErrorType.NotFound);
            }

            if (request.ClassGroupId is not null
                && await _classGroups.GetByIdAsync(request.ClassGroupId.Value, cancellationToken) is null)
                return OperationResult<int>.Failure("الفوج غير موجود.", ErrorType.NotFound);

            // فرادة الفعّالة على نفس النطاق — الفهارس تضمن، والفحص يعطي الرسالة النظيفة
            PayPolicy? activeExisting;
            string scopeMessage;
            if (request.PayeeKind == PayeeKind.Teacher && request.ClassGroupId is null)
            {
                activeExisting = await _policies.GetActiveDefaultForTeacherAsync(request.TeacherId!.Value, cancellationToken);
                scopeMessage = "لهذا الأستاذ سياسة افتراضية فعّالة — عدّلها أو عطّلها أولاً.";
            }
            else if (request.PayeeKind == PayeeKind.Teacher)
            {
                activeExisting = await _policies.GetActiveOverrideAsync(request.TeacherId!.Value, request.ClassGroupId!.Value, cancellationToken);
                scopeMessage = "لهذا الأستاذ تجاوز فعّال على هذا الفوج — عدّله أو عطّله أولاً.";
            }
            else
            {
                activeExisting = await _policies.GetActiveForEmployeeAsync(request.EmployeeId!.Value, cancellationToken);
                scopeMessage = "لهذا الموظف سياسة فعّالة — عدّلها أو عطّلها أولاً.";
            }

            if (activeExisting is not null)
                return OperationResult<int>.Failure(scopeMessage, ErrorType.Conflict);

            var policy = PayPolicy.Create(request.PayeeKind, request.TeacherId, request.EmployeeId, request.ClassGroupId,
                request.Kind, request.RateCentimes, request.Percentage, request.CountsUnjustifiedAbsent,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _policies.AddAsync(policy, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(policy.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while creating pay policy {PayeeKind} (teacher {TeacherId} / employee {EmployeeId} / group {ClassGroupId}) — temporary diagnostics (B-2 incident)", request.PayeeKind, request.TeacherId, request.EmployeeId, request.ClassGroupId);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create pay policy for {PayeeKind} (teacher {TeacherId} / employee {EmployeeId} / group {ClassGroupId})",
                request.PayeeKind, request.TeacherId, request.EmployeeId, request.ClassGroupId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء سياسة الأجر.", ErrorType.Unexpected);
        }
    }
}