using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Employees;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Employees;

/// <summary>ملف موظف فوق شخص قائم — زر «أضف كموظف» من شاشة الأشخاص (مرآة CreateStudentFileHandler — D-115)</summary>
public sealed record CreateEmployeeFileRequest(int PersonId, string? JobTitle, string? Notes);

public sealed class CreateEmployeeFileHandler
{
    private readonly IPersonRepository _persons;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEmployeeFileHandler> _logger;

    public CreateEmployeeFileHandler(
        IPersonRepository persons,
        IEmployeeRepository employees,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateEmployeeFileHandler> logger)
    {
        _persons = persons;
        _employees = employees;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateEmployeeFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.JobTitle))
            return OperationResult<int>.Failure("أدخل وظيفة الموظف.", ErrorType.Validation);

        try
        {
            var person = await _persons.GetByIdAsync(request.PersonId, cancellationToken);
            if (person is null)
                return OperationResult<int>.Failure("الشخص غير موجود.", ErrorType.NotFound);
            if (!person.IsActive)
                return OperationResult<int>.Failure("لا يمكن إنشاء ملف لشخص معطّل — فعّله أولاً.", ErrorType.BusinessRule);

            // الفهرس المفلتر يضمن القاعدة، وهذا الفحص يعطي الرسالة النظيفة (D-22)
            if (await _employees.AnyActiveForPersonAsync(request.PersonId, cancellationToken))
                return OperationResult<int>.Failure("لهذا الشخص ملف موظف فعّال بالفعل.", ErrorType.Conflict);

            var employee = Employee.Create(request.PersonId, request.JobTitle!, request.Notes,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _employees.AddAsync(employee, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(employee.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create employee file for person {PersonId}", request.PersonId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء ملف الموظف.", ErrorType.Unexpected);
        }
    }
}