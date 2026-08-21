using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record DeactivateSubjectRequest(int SubjectId);

public sealed class DeactivateSubjectHandler
{
    private readonly ISubjectRepository _subjects;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateSubjectHandler> _logger;

    public DeactivateSubjectHandler(ISubjectRepository subjects, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<DeactivateSubjectHandler> logger)
    {
        _subjects = subjects;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(DeactivateSubjectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var subject = await _subjects.GetByIdAsync(request.SubjectId, cancellationToken);
            if (subject is null)
                return OperationResult.Failure("المادة غير موجودة.", ErrorType.NotFound);

            // ح-5: حارس البيانات التشغيلية — يُفعَّل فعلياً في F2 (الأفواج)
            if (await _subjects.HasOperationalDataAsync(request.SubjectId, cancellationToken))
                return OperationResult.Failure("لا يمكن تعطيل مادة عليها بيانات تشغيلية (أفواج…) — تبقى للأرشيف.", ErrorType.BusinessRule);

            subject.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _subjects.UpdateAsync(subject, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deactivate subject {SubjectId}", request.SubjectId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل المادة.", ErrorType.Unexpected);
        }
    }
}