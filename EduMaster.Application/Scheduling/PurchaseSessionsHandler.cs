using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>شراء حصص لتسجيل فوج (D-91) — على النشط فقط (D-99) · كمية بلا مبلغ (D-96) · مستحق الحزمة ذرّياً (D-103)</summary>
public sealed record PurchaseSessionsRequest(int ClassGroupEnrollmentId, int SessionsCount, string? Note);

public sealed class PurchaseSessionsHandler
{
    private readonly IGroupSessionPurchaseRepository _purchases;
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly IChargeRepository _charges;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreditConsumptionService _creditConsumption;
    private readonly ILogger<PurchaseSessionsHandler> _logger;

    public PurchaseSessionsHandler(IGroupSessionPurchaseRepository purchases, IClassGroupEnrollmentRepository groupEnrollments,
        IClassGroupRepository classGroups, IAcademicYearRepository academicYears,
        IChargeRepository charges,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork,
        CreditConsumptionService creditConsumption, ILogger<PurchaseSessionsHandler> logger)
    {
        _purchases = purchases;
        _groupEnrollments = groupEnrollments;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _charges = charges;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _creditConsumption = creditConsumption;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(PurchaseSessionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SessionsCount <= 0)
            return OperationResult<int>.Failure("عدد الحصص المشتراة يجب أن يكون أكبر من صفر.", ErrorType.Validation);

        try
        {
            var enrollment = await _groupEnrollments.GetByIdAsync(request.ClassGroupEnrollmentId, cancellationToken);
            if (enrollment is null)
                return OperationResult<int>.Failure("التسجيل غير موجود.", ErrorType.NotFound);

            // D-99: شراء على نشط فقط — المنسحب يُعاد إلحاقه أولاً (صف جديد D-53)
            if (!enrollment.IsActive)
                return OperationResult<int>.Failure("لا يمكن شراء حصص لتسجيل منسحب — أعد إلحاقه بالفوج أولاً.", ErrorType.BusinessRule);

            // حارس السنة الحالية: شراء الحصص عملية تشغيلية للسنة الحالية فقط.
            // التحقق يتم خلف الواجهة وقبل فتح المعاملة، حتى لا يمكن تجاوز UI.
            var currentAcademicYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentAcademicYear is null)
                return OperationResult<int>.Failure("لا توجد سنة دراسية حالية مضبوطة في النظام.", ErrorType.Unexpected);

            var group = await _classGroups.GetByIdAsync(enrollment.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult<int>.Failure("الفوج المرتبط بالتسجيل غير موجود.", ErrorType.Unexpected);

            if (group.AcademicYearId != currentAcademicYear.Id)
                return OperationResult<int>.Failure("لا يمكن شراء حصص لتسجيل تابع لسنة دراسية سابقة. أعد الإلحاق بالفوج ضمن السنة الحالية أولاً.", ErrorType.BusinessRule);

            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            var purchase = Domain.Scheduling.GroupSessionPurchase.Create(
                request.ClassGroupEnrollmentId, request.SessionsCount, request.Note,
                utcNow, userId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _purchases.AddAsync(purchase, cancellationToken);

            // D-103/D-96: مستحق الحزمة في نفس المعاملة = عدد × سعر الحصة المتفق (المسنابشوت) — يُتخطّى عند 0 (مجاني صريح)
            var bundleAmountCentimes = request.SessionsCount * enrollment.AgreedUnitPriceCentimes;
            if (bundleAmountCentimes > 0)
            {
                var charge = Domain.Billing.Charge.CreateForSessionBundle(
                    enrollment.StudentId, purchase.Id, bundleAmountCentimes, utcNow, userId);
                await _charges.AddAsync(charge, cancellationToken);
            }

            // 6.6 — ز-1: الزائدة الدائنة (إن وُجدت) تستهلَك فوراً في مستحق الحزمة الجديد وسابقه المفتوح — سداد وعد D-107 داخل المعاملة ذاتها
            await _creditConsumption.ConsumeForStudentAsync(enrollment.StudentId, utcNow, userId, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(purchase.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to purchase {SessionsCount} sessions for group enrollment {ClassGroupEnrollmentId}",
                request.SessionsCount, request.ClassGroupEnrollmentId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء شراء الحصص.", ErrorType.Unexpected);
        }
    }
}