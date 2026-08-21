using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Academic;

public sealed record CreateSubjectRequest(string? Name);

public sealed class CreateSubjectHandler
{
    private readonly ISubjectRepository _subjects;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateSubjectHandler> _logger;

    public CreateSubjectHandler(ISubjectRepository subjects, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateSubjectHandler> logger)
    {
        _subjects = subjects;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateSubjectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult<int>.Failure("أدخل اسم المادة.", ErrorType.Validation);

        try
        {
            // فحص الفرادة الودي قبل الاصطدام بالقيد (D-22)
            if (await _subjects.AnyWithNameAsync(request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("توجد مادة بهذا الاسم بالفعل.", ErrorType.Conflict);

            var subject = Subject.Create(request.Name, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _subjects.AddAsync(subject, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(subject.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create subject {Name}", request.Name);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة المادة.", ErrorType.Unexpected);
        }
    }
}