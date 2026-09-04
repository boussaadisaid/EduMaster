using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>الانسحاب من فوج (D-53) — الأثر المالي (رصيد الحصص/الاسترجاع) موضوع F4 (UC-30)</summary>
public sealed record WithdrawGroupEnrollmentRequest(int GroupEnrollmentId);

public sealed class WithdrawGroupEnrollmentHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WithdrawGroupEnrollmentHandler> _logger;

    public WithdrawGroupEnrollmentHandler(IClassGroupEnrollmentRepository groupEnrollments, IClassGroupRepository classGroups, IAcademicYearRepository academicYears, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<WithdrawGroupEnrollmentHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(WithdrawGroupEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var enrollment = await _groupEnrollments.GetByIdAsync(request.GroupEnrollmentId, cancellationToken);
            if (enrollment is null)
                return OperationResult.Failure("التسجيل غير موجود.", ErrorType.NotFound);

            var group = await _classGroups.GetByIdAsync(enrollment.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult.Failure("فوج التسجيل غير موجود.", ErrorType.NotFound);
            if (group.AcademicYearId != currentYear.Id)
                return OperationResult.Failure("لا يمكن تسجيل الانسحاب من سنة دراسية سابقة أو غير حالية من شاشة التشغيل الحالية.", ErrorType.BusinessRule);

            enrollment.Withdraw(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _groupEnrollments.UpdateAsync(enrollment, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to withdraw group enrollment {GroupEnrollmentId}", request.GroupEnrollmentId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تسجيل الانسحاب من الفوج.", ErrorType.Unexpected);
        }
    }
}