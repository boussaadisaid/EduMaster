using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using EduMaster.Domain.Teachers;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Teachers;

public sealed record CreateTeacherRequest(
    string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    string? Specialty, string? Notes,
    string? PhotoSourcePath = null);   // null = بلا صورة

public sealed class CreateTeacherHandler
{
    private readonly IPersonRepository _persons;
    private readonly ITeacherRepository _teachers;
    private readonly IImageStore _imageStore;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateTeacherHandler> _logger;

    public CreateTeacherHandler(
        IPersonRepository persons,
        ITeacherRepository teachers,
        IImageStore imageStore,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateTeacherHandler> logger)
    {
        _persons = persons;
        _teachers = teachers;
        _imageStore = imageStore;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateTeacherRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return OperationResult<int>.Failure("أدخل الاسم الأول واللقب.", ErrorType.Validation);

        try
        {
            var utcNow = _clock.UtcNow;
            var today = DateOnly.FromDateTime(utcNow);

            // الصورة أولاً — نسخ الملف قبل فتح المعاملة (لا نحجز القاعدة أثناء IO)
            string? storedPhoto = null;
            if (!string.IsNullOrWhiteSpace(request.PhotoSourcePath))
            {
                try
                {
                    storedPhoto = await _imageStore.SaveFromPathAsync(request.PhotoSourcePath, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return OperationResult<int>.Failure("الصورة غير مدعومة أو يتجاوز حجمها 5MB — المسموح: jpg / png.", ErrorType.Validation);
                }
            }

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
                photoPath: storedPhoto,
                createdAtUtc: utcNow,
                createdByUserId: _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            await _persons.AddAsync(person, cancellationToken);
            // SetId ملأ person.Id في نفس الرحلة — جاهز للملف
            var teacher = Teacher.Create(person.Id, request.Specialty, request.Notes,
                utcNow, _currentUser.UserAccountId);

            await _teachers.AddAsync(teacher, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(teacher.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create teacher {FirstName} {LastName}", request.FirstName, request.LastName);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة الأستاذ.", ErrorType.Unexpected);
        }
    }
}