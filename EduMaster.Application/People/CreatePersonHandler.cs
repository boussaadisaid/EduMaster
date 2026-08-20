using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using Microsoft.Extensions.Logging;




namespace EduMaster.Application.People;


public sealed class CreatePersonHandler
{
    private readonly IPersonRepository _persons;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreatePersonHandler> _logger;

    public CreatePersonHandler(
        IPersonRepository persons,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreatePersonHandler> logger)
    {
        _persons = persons;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreatePersonRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return OperationResult<int>.Failure("أدخل الاسم الأول.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(request.LastName))
            return OperationResult<int>.Failure("أدخل اللقب.", ErrorType.Validation);

        try
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);

            // الـVOs تملك القواعد وترمي DomainException عربية (D-19)
            var person = Person.Create(
                firstName: new FirstName(request.FirstName),
                lastName: new LastName(request.LastName),
                fatherName: string.IsNullOrWhiteSpace(request.FatherName) ? null : new FirstName(request.FatherName),
                birthDate: request.BirthDate is null ? null : BirthDate.Create(request.BirthDate.Value, today),
                gender: request.Gender,
                phone: string.IsNullOrWhiteSpace(request.Phone) ? null : new Phone(request.Phone),
                phone2: string.IsNullOrWhiteSpace(request.Phone2) ? null : new Phone(request.Phone2),
                email: string.IsNullOrWhiteSpace(request.Email) ? null : new Email(request.Email),
                address: request.Address,
                photoPath: request.PhotoPath,
                createdAtUtc: _clock.UtcNow,
                createdByUserId: _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _persons.AddAsync(person, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(person.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create person {FirstName} {LastName}", request.FirstName, request.LastName);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة الشخص.", ErrorType.Unexpected);
        }
    }
}