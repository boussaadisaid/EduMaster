using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>D-53: الانسحاب السنوي يسحب كل تسجيلات الأفواج النشطة في نفس المعاملة (كاسكيد منطقي — مفعَّل منذ 2.4)</summary>
public sealed record WithdrawAnnualEnrollmentRequest(int EnrollmentId);

public sealed class WithdrawAnnualEnrollmentHandler
{
    private readonly IAnnualEnrollmentRepository _enrollments;
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WithdrawAnnualEnrollmentHandler> _logger;

    public WithdrawAnnualEnrollmentHandler(IAnnualEnrollmentRepository enrollments,
        IClassGroupEnrollmentRepository groupEnrollments, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<WithdrawAnnualEnrollmentHandler> logger)
    {
        _enrollments = enrollments;
        _groupEnrollments = groupEnrollments;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(WithdrawAnnualEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var enrollment = await _enrollments.GetByIdAsync(request.EnrollmentId, cancellationToken);
            if (enrollment is null)
                return OperationResult.Failure("التسجيل غير موجود.", ErrorType.NotFound);

            // D-53: كل تسجيلات الأفواج النشطة تُسحب معه — نفس اللحظة ونفس المعاملة
            var activeGroupEnrollments = await _groupEnrollments.GetActiveByAnnualEnrollmentIdAsync(enrollment.Id, cancellationToken);

            var now = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            enrollment.Withdraw(now, userId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _enrollments.UpdateAsync(enrollment, cancellationToken);
            foreach (var groupEnrollment in activeGroupEnrollments)
            {
                groupEnrollment.Withdraw(now, userId);
                await _groupEnrollments.UpdateAsync(groupEnrollment, cancellationToken);
            }
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to withdraw annual enrollment {EnrollmentId}", request.EnrollmentId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تسجيل الانسحاب.", ErrorType.Unexpected);
        }
    }
}