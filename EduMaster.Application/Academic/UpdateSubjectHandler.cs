using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record UpdateSubjectRequest(int SubjectId, string? Name);

public sealed class UpdateSubjectHandler
{
    private readonly ISubjectRepository _subjects;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSubjectHandler> _logger;

    public UpdateSubjectHandler(ISubjectRepository subjects, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateSubjectHandler> logger)
    {
        _subjects = subjects;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateSubjectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult.Failure("أدخل اسم المادة.", ErrorType.Validation);

        try
        {
            var subject = await _subjects.GetByIdAsync(request.SubjectId, cancellationToken);
            if (subject is null)
                return OperationResult.Failure("المادة غير موجودة.", ErrorType.NotFound);

            // فرادة مع استثناء الذات (نمط D-27)
            if (await _subjects.AnyWithNameAsync(request.Name.Trim(), request.SubjectId, cancellationToken))
                return OperationResult.Failure("توجد مادة أخرى بهذا الاسم بالفعل.", ErrorType.Conflict);

            subject.Update(request.Name, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _subjects.UpdateAsync(subject, cancellationToken);
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
            _logger.LogError(ex, "Failed to update subject {SubjectId}", request.SubjectId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل المادة.", ErrorType.Unexpected);
        }
    }
}