using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Teachers;

public sealed record SoftDeleteTeacherRequest(int TeacherId);

public sealed class SoftDeleteTeacherHandler
{
    private readonly ITeacherRepository _teachers;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SoftDeleteTeacherHandler> _logger;

    public SoftDeleteTeacherHandler(
        ITeacherRepository teachers,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<SoftDeleteTeacherHandler> logger)
    {
        _teachers = teachers;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(SoftDeleteTeacherRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var teacher = await _teachers.GetByIdAsync(request.TeacherId, cancellationToken);
            if (teacher is null)
                return OperationResult.Failure("ملف الأستاذ غير موجود.", ErrorType.NotFound);

            // ح-7: الإزالة للأخطاء فقط — ملف عليه بيانات تشغيلية يبقى للأرشيف (يُفعَّل الفحص في F2/F5)
            if (await _teachers.HasOperationalDataAsync(request.TeacherId, cancellationToken))
                return OperationResult.Failure("لا يمكن إزالة ملف عليه بيانات تشغيلية (مستحقات/تسجيلات…). يبقى للأرشيف — ويمكنك تعطيل الشخص بدلاً من ذلك.", ErrorType.BusinessRule);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _teachers.SoftDeleteAsync(request.TeacherId, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Admin {AdminUserId} soft-deleted teacher file {TeacherId}", _currentUser.UserAccountId, request.TeacherId);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to soft-delete teacher {TeacherId}", request.TeacherId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء إزالة ملف الأستاذ.", ErrorType.Unexpected);
        }
    }
}