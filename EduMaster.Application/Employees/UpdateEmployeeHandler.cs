using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.People.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Employees;

/// <summary>تعديل موظف: حقول الشخص + الوظيفة/الملاحظات في معاملة واحدة (مرآة UpdateTeacherHandler)</summary>
public sealed record UpdateEmployeeRequest(
    int EmployeeId,
    string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    string? JobTitle, string? Notes);

public sealed class UpdateEmployeeHandler
{
    private readonly IPersonRepository _persons;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateEmployeeHandler> _logger;

    public UpdateEmployeeHandler(
        IPersonRepository persons,
        IEmployeeRepository employees,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<UpdateEmployeeHandler> logger)
    {
        _persons = persons;
        _employees = employees;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return OperationResult.Failure("أدخل الاسم الأول واللقب.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(request.JobTitle))
            return OperationResult.Failure("أدخل وظيفة الموظف.", ErrorType.Validation);

        try
        {
            var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
                return OperationResult.Failure("ملف الموظف غير موجود.", ErrorType.NotFound);

            var person = await _persons.GetByIdAsync(employee.PersonId, cancellationToken);
            if (person is null)
            {
                _logger.LogError("Employee {EmployeeId} references missing person {PersonId}", request.EmployeeId, employee.PersonId);
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

            employee.Update(request.JobTitle!, request.Notes, utcNow, _currentUser.UserAccountId);   // مضمونة غير فارغة بالحارس أعلاه

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _persons.UpdateAsync(person, cancellationToken);
            await _employees.UpdateAsync(employee, cancellationToken);
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
            _logger.LogError(ex, "Failed to update employee {EmployeeId}", request.EmployeeId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الموظف.", ErrorType.Unexpected);
        }
    }
}