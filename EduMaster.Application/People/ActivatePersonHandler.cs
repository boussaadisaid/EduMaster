using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.People;


public sealed class ActivatePersonHandler
{
    private readonly IPersonRepository _persons;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivatePersonHandler> _logger;

    public ActivatePersonHandler(
        IPersonRepository persons,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<ActivatePersonHandler> logger)
    {
        _persons = persons;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(ActivatePersonRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var person = await _persons.GetByIdAsync(request.PersonId, cancellationToken);
            if (person is null)
                return OperationResult.Failure("الشخص غير موجود.", ErrorType.NotFound);

            if (person.IsActive)
                return OperationResult.Success();   // فعّال أصلاً — لا كتابة بلا معنى

            person.Activate(_clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _persons.UpdateAsync(person, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to activate person {PersonId}", request.PersonId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل الشخص.", ErrorType.Unexpected);
        }
    }
}