using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record ActivateSubjectRequest(int SubjectId);

public sealed class ActivateSubjectHandler
{
    private readonly ISubjectRepository _subjects;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateSubjectHandler> _logger;

    public ActivateSubjectHandler(ISubjectRepository subjects, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateSubjectHandler> logger)
    {
        _subjects = subjects;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivateSubjectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var subject = await _subjects.GetByIdAsync(request.SubjectId, cancellationToken);
            if (subject is null)
                return OperationResult.Failure("المادة غير موجودة.", ErrorType.NotFound);

            // التفعيل دائم الجواز — لا حارس تشغيلية هنا
            subject.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _subjects.UpdateAsync(subject, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate subject {SubjectId}", request.SubjectId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل المادة.", ErrorType.Unexpected);
        }
    }
}