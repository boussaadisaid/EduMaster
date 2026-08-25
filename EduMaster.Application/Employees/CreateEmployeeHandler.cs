using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Employees;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People;
using EduMaster.Domain.People.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Employees;

/// <summary>إنشاء موظف = شخص + ملف في معاملة واحدة (مرآة CreateTeacherHandler — D-115)</summary>
public sealed record CreateEmployeeRequest(
    string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    string? JobTitle, string? Notes,
    string? PhotoSourcePath = null);   // null = بلا صورة

public sealed class CreateEmployeeHandler
{
    private readonly IPersonRepository _persons;
    private readonly IEmployeeRepository _employees;
    private readonly IImageStore _imageStore;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEmployeeHandler> _logger;

    public CreateEmployeeHandler(
        IPersonRepository persons,
        IEmployeeRepository employees,
        IImageStore imageStore,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateEmployeeHandler> logger)
    {
        _persons = persons;
        _employees = employees;
        _imageStore = imageStore;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return OperationResult<int>.Failure("أدخل الاسم الأول واللقب.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(request.JobTitle))
            return OperationResult<int>.Failure("أدخل وظيفة الموظف.", ErrorType.Validation);

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
            var employee = Employee.Create(person.Id, request.JobTitle!, request.Notes,   // مضمونة غير فارغة بالحارس أعلاه
                utcNow, _currentUser.UserAccountId);

            await _employees.AddAsync(employee, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(employee.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while creating employee {FirstName} {LastName} — temporary diagnostics (B-1 incident)", request.FirstName, request.LastName);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create employee {FirstName} {LastName}", request.FirstName, request.LastName);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة الموظف.", ErrorType.Unexpected);
        }
    }
}