using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using EduMaster.Domain.Students;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Students;

public sealed record CreateStudentRequest(
    string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    int? GuardianPersonId, StudentCategory Category, string? Notes,
    string? PhotoSourcePath = null);   // مسار اختاره المستخدم — null = بلا صورة

public sealed class CreateStudentHandler
{
    private readonly IPersonRepository _persons;
    private readonly IStudentRepository _students;
    private readonly IImageStore _imageStore;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateStudentHandler> _logger;

    public CreateStudentHandler(
        IPersonRepository persons,
        IStudentRepository students,
        IImageStore imageStore,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateStudentHandler> logger)
    {
        _persons = persons;
        _students = students;
        _imageStore = imageStore;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateStudentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return OperationResult<int>.Failure("أدخل الاسم الأول واللقب.", ErrorType.Validation);

        try
        {
            var utcNow = _clock.UtcNow;
            var today = DateOnly.FromDateTime(utcNow);

            // ① الصورة أولاً — نسخ الملف قبل فتح المعاملة (لا نحجز القاعدة أثناء IO)
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

            // ② ولي الأمر إن حُدد: موجود وفعّال
            if (request.GuardianPersonId is not null)
            {
                var guardian = await _persons.GetByIdAsync(request.GuardianPersonId.Value, cancellationToken);
                if (guardian is null)
                    return OperationResult<int>.Failure("ولي الأمر المحدد غير موجود.", ErrorType.Validation);
                if (!guardian.IsActive)
                    return OperationResult<int>.Failure("ولي الأمر المحدد معطّل — فعّله أولاً.", ErrorType.BusinessRule);
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

            // ③ معاملة واحدة تحيط بالكتابتين — Commit أو Rollback على كل مسار
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            await _persons.AddAsync(person, cancellationToken);
            // ⭐ SetId ملأ person.Id في نفس الرحلة — فيصبح جاهزاً للملف
            var student = Student.Create(
                personId: person.Id,
                guardianPersonId: request.GuardianPersonId,
                category: request.Category,
                notes: request.Notes,
                createdAtUtc: utcNow,
                createdByUserId: _currentUser.UserAccountId);

            await _students.AddAsync(student, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(student.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create student {FirstName} {LastName}", request.FirstName, request.LastName);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة الطالب.", ErrorType.Unexpected);
        }
    }
}