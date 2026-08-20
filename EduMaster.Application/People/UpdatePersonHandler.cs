using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.People.ValueObjects;
using Microsoft.Extensions.Logging;




namespace EduMaster.Application.People
{
   

    public sealed class UpdatePersonHandler
    {
        private readonly IPersonRepository _persons;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdatePersonHandler> _logger;

        public UpdatePersonHandler(
       IPersonRepository persons, IClock clock, ICurrentUserService currentUser,
       IUnitOfWork unitOfWork, ILogger<UpdatePersonHandler> logger)
        {
            _persons = persons;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OperationResult> ExecuteAsync(UpdatePersonRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.FirstName))
                return OperationResult.Failure("أدخل الاسم الأول.", ErrorType.Validation);
            if (string.IsNullOrWhiteSpace(request.LastName))
                return OperationResult.Failure("أدخل اللقب.", ErrorType.Validation);

            try
            {
                var person = await _persons.GetByIdAsync(request.Id, cancellationToken);
                if (person is null)
                    return OperationResult.Failure("الشخص غير موجود.", ErrorType.NotFound);

                var today = DateOnly.FromDateTime(_clock.UtcNow);

                person.Update(
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
                    updatedAtUtc: _clock.UtcNow,
                    updatedByUserId: _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _persons.UpdateAsync(person, cancellationToken);
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
                _logger.LogError(ex, "Failed to update person {PersonId}", request.Id);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الشخص.", ErrorType.Unexpected);
            }
        }
    }
}
