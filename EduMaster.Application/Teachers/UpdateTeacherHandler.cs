using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Teachers;

public sealed record UpdateTeacherRequest(
    int TeacherId,
    string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    string? Specialty, string? Notes);

public sealed class UpdateTeacherHandler
{
    private readonly IPersonRepository _persons;
    private readonly ITeacherRepository _teachers;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateTeacherHandler> _logger;

    public UpdateTeacherHandler(
        IPersonRepository persons,
        ITeacherRepository teachers,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<UpdateTeacherHandler> logger)
    {
        _persons = persons;
        _teachers = teachers;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateTeacherRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return OperationResult.Failure("أدخل الاسم الأول واللقب.", ErrorType.Validation);

        try
        {
            var teacher = await _teachers.GetByIdAsync(request.TeacherId, cancellationToken);
            if (teacher is null)
                return OperationResult.Failure("ملف الأستاذ غير موجود.", ErrorType.NotFound);

            var person = await _persons.GetByIdAsync(teacher.PersonId, cancellationToken);
            if (person is null)
            {
                _logger.LogError("Teacher {TeacherId} references missing person {PersonId}", request.TeacherId, teacher.PersonId);
                return OperationResult.Failure("بيانات غير متسقة: ملف بلا شخص. راجع الدعم.", ErrorType.Unexpected);
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

            teacher.Update(request.Specialty, request.Notes, utcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _persons.UpdateAsync(person, cancellationToken);
            await _teachers.UpdateAsync(teacher, cancellationToken);
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
            _logger.LogError(ex, "Failed to update teacher {TeacherId}", request.TeacherId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الأستاذ.", ErrorType.Unexpected);
        }
    }
}