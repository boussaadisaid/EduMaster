using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>
/// النقل بين الأفواج (D-53/D-78): انسحاب من الحالي + إلحاق بالهدف في معاملة واحدة بسنابشوت الهدف.
/// الحُراس تعكس EnrollStudentInGroupHandler عمداً (تطابق مستوى/شعبة/سعة/فرادة) — أي تعديل هناك يُعكَس هنا.
/// الأثر المالي لنقل الرصيد/الاسترجاع موضوع F4 (UC-30).
/// </summary>
public sealed record TransferGroupEnrollmentRequest(
    int GroupEnrollmentId,
    int TargetClassGroupId,
    string? DiscountNote);

public sealed class TransferGroupEnrollmentHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAnnualEnrollmentRepository _annualEnrollments;
    private readonly IAcademicYearRepository _academicYears;
    private readonly ISubjectPriceRepository _prices;
    private readonly ISessionBalanceRepository _sessionBalances;
    private readonly IGroupSessionTransferRepository _sessionTransfers;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferGroupEnrollmentHandler> _logger;

    public TransferGroupEnrollmentHandler(IClassGroupEnrollmentRepository groupEnrollments, IClassGroupRepository classGroups,
        IAnnualEnrollmentRepository annualEnrollments, IAcademicYearRepository academicYears, ISubjectPriceRepository prices,
        ISessionBalanceRepository sessionBalances, IGroupSessionTransferRepository sessionTransfers, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<TransferGroupEnrollmentHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _classGroups = classGroups;
        _annualEnrollments = annualEnrollments;
        _academicYears = academicYears;
        _prices = prices;
        _sessionBalances = sessionBalances;
        _sessionTransfers = sessionTransfers;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(TransferGroupEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult<int>.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var current = await _groupEnrollments.GetByIdAsync(request.GroupEnrollmentId, cancellationToken);
            if (current is null)
                return OperationResult<int>.Failure("التسجيل غير موجود.", ErrorType.NotFound);
            if (!current.IsActive)
                return OperationResult<int>.Failure("لا يمكن نقل تسجيل منسحب.", ErrorType.BusinessRule);

            var source = await _classGroups.GetByIdAsync(current.ClassGroupId, cancellationToken);
            if (source is null)
                return OperationResult<int>.Failure("الفوج الحالي للتسجيل غير موجود.", ErrorType.NotFound);
            if (source.AcademicYearId != currentYear.Id)
                return OperationResult<int>.Failure("لا يمكن نقل تسجيل من سنة دراسية سابقة أو غير حالية من شاشة التشغيل الحالية.", ErrorType.BusinessRule);

            if (request.TargetClassGroupId == current.ClassGroupId)
                return OperationResult<int>.Failure("الفوج الهدف هو فوج الطالب الحالي.", ErrorType.Validation);

            var target = await _classGroups.GetByIdAsync(request.TargetClassGroupId, cancellationToken);
            if (target is null)
                return OperationResult<int>.Failure("الفوج الهدف غير موجود.", ErrorType.Validation);
            if (target.AcademicYearId != currentYear.Id)
                return OperationResult<int>.Failure("الفوج الهدف لا ينتمي إلى السنة الدراسية الحالية.", ErrorType.BusinessRule);
            if (!target.IsActive)
                return OperationResult<int>.Failure("الفوج الهدف معطّل — لا يقبل تسجيلات.", ErrorType.BusinessRule);

            // التسجيل السنوي المرتبط بالحالي هو المرجع — النقل داخل نفس سنته
            var annual = await _annualEnrollments.GetByIdAsync(current.AnnualEnrollmentId, cancellationToken);
            if (annual is null)
                return OperationResult<int>.Failure("التسجيل السنوي المرتبط غير موجود.", ErrorType.Unexpected);
            if (!annual.IsActive)
                return OperationResult<int>.Failure("التسجيل السنوي للطالب لم يعد نشطاً — لا نقل.", ErrorType.BusinessRule);

            if (annual.LevelId != target.LevelId)
                return OperationResult<int>.Failure("مستوى الفوج الهدف لا يطابق مستوى الطالب في تسجيله السنوي.", ErrorType.BusinessRule);

            var targetStreamIds = await _classGroups.GetStreamIdsAsync(target.Id, cancellationToken);
            if (targetStreamIds.Count > 0)
            {
                if (annual.StreamId is null)
                    return OperationResult<int>.Failure("الفوج الهدف مقيّد بشعب محددة والطالب بلا شعبة في تسجيله السنوي.", ErrorType.BusinessRule);
                if (!targetStreamIds.Contains(annual.StreamId.Value))
                    return OperationResult<int>.Failure("شعبة الطالب ليست ضمن شعب الفوج الهدف.", ErrorType.BusinessRule);
            }

            if (await _groupEnrollments.AnyActiveForStudentInGroupAsync(target.Id, current.StudentId, cancellationToken))
                return OperationResult<int>.Failure("الطالب مسجَّل أصلاً في الفوج الهدف.", ErrorType.Conflict);

            if (target.Capacity is not null
                && await _groupEnrollments.CountActiveInGroupAsync(target.Id, cancellationToken) >= target.Capacity.Value)
                return OperationResult<int>.Failure($"الفوج الهدف ممتلئ (سعته {target.Capacity.Value}).", ErrorType.BusinessRule);

            // D-77 على سنابشوت الهدف
            var targetPrice = await _prices.TryGetPriceAsync(target.AcademicYearId, target.LevelId, target.SubjectId, cancellationToken);
            if (targetPrice is null)
                return OperationResult<int>.Failure("لا يوجد سعر محدد للفوج الهدف في جدول الأسعار؛ لا يمكن التحقق من تطابق السعر.", ErrorType.Validation);

            if (targetPrice.Value != current.AgreedUnitPriceCentimes)
                return OperationResult<int>.Failure(
                    $"لا يمكن نقل رصيد الحصص لأن سعر الحصة مختلف. السعر الحالي: {current.AgreedUnitPriceCentimes} سنتيم، وسعر الفوج الهدف: {targetPrice.Value} سنتيم.",
                    ErrorType.BusinessRule);

            var now = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var balance = await _sessionBalances.GetAsync(current.Id, cancellationToken);
            if (balance is null)
                throw new InvalidOperationException($"Session balance could not be read for group enrollment {current.Id}.");

            var transferableSessions = Math.Max(0, balance.Balance);

            current.Withdraw(now, userId);
            var transferred = Domain.Enrollments.ClassGroupEnrollment.Create(
                target.Id, current.StudentId, current.AnnualEnrollmentId,
                targetPrice.Value, targetPrice.Value, request.DiscountNote,
                now, userId);

            await _groupEnrollments.UpdateAsync(current, cancellationToken);
            await _groupEnrollments.AddAsync(transferred, cancellationToken);

            if (transferableSessions > 0)
            {
                var transfer = Domain.Scheduling.GroupSessionTransfer.Create(
                    current.Id, transferred.Id, transferableSessions,
                    "نقل رصيد الحصص مع نقل الطالب", now, userId);
                await _sessionTransfers.AddAsync(transfer, cancellationToken);
            }

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(transferred.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to transfer group enrollment {GroupEnrollmentId} to class group {TargetClassGroupId}",
                request.GroupEnrollmentId, request.TargetClassGroupId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء النقل بين الأفواج.", ErrorType.Unexpected);
        }
    }
}