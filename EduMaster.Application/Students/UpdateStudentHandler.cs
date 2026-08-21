using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People.ValueObjects;
using EduMaster.Domain.Students;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.Students
{
    public sealed record UpdateStudentRequest(
    int StudentId,
    string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    int? GuardianPersonId, StudentCategory Category, string? Notes);

    public sealed class UpdateStudentHandler
    {
        // نفس اعتماديات CreateStudentHandler ما عدا IImageStore — والـctor القياسي
        private readonly IPersonRepository _persons;
        private readonly IStudentRepository _students;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateStudentHandler> _logger;

        public UpdateStudentHandler(
            IPersonRepository persons,
            IStudentRepository students,
            IClock clock,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork,
            ILogger<UpdateStudentHandler> logger)
        {
            _persons = persons;
            _students = students;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }


        public async Task<OperationResult> ExecuteAsync(UpdateStudentRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                return OperationResult.Failure("أدخل الاسم الأول واللقب.", ErrorType.Validation);

            try
            {
                var student = await _students.GetByIdAsync(request.StudentId, cancellationToken);
                if (student is null)
                    return OperationResult.Failure("ملف الطالب غير موجود.", ErrorType.NotFound);

                var person = await _persons.GetByIdAsync(student.PersonId, cancellationToken);
                if (person is null)
                {
                    _logger.LogError("Student {StudentId} references missing person {PersonId}", request.StudentId, student.PersonId);
                    return OperationResult.Failure("بيانات غير متسقة: ملف بلا شخص. راجع الدعم.", ErrorType.Unexpected);
                }

                if (request.GuardianPersonId is not null)
                {
                    var guardian = await _persons.GetByIdAsync(request.GuardianPersonId.Value, cancellationToken);
                    if (guardian is null)
                        return OperationResult.Failure("ولي الأمر المحدد غير موجود.", ErrorType.Validation);
                    if (!guardian.IsActive)
                        return OperationResult.Failure("ولي الأمر المحدد معطّل — فعّله أولاً.", ErrorType.BusinessRule);
                }

                var utcNow = _clock.UtcNow;
                var today = DateOnly.FromDateTime(utcNow);

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
                    photoPath: person.PhotoPath,   // الصورة تُدار عبر SetPersonPhotoHandler — التعديل يحافظ عليها
                    updatedAtUtc: utcNow,
                    updatedByUserId: _currentUser.UserAccountId);

                student.Update(request.GuardianPersonId, request.Category, request.Notes,
                    utcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _persons.UpdateAsync(person, cancellationToken);
                await _students.UpdateAsync(student, cancellationToken);
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
                _logger.LogError(ex, "Failed to update student {StudentId}", request.StudentId);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الطالب.", ErrorType.Unexpected);
            }
        }
    }
}
