using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Teachers;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Teachers;

public sealed record CreateTeacherFileRequest(int PersonId, string? Specialty, string? Notes);

public sealed class CreateTeacherFileHandler
{
    private readonly IPersonRepository _persons;
    private readonly ITeacherRepository _teachers;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateTeacherFileHandler> _logger;

    public CreateTeacherFileHandler(
        IPersonRepository persons,
        ITeacherRepository teachers,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateTeacherFileHandler> logger)
    {
        _persons = persons;
        _teachers = teachers;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateTeacherFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var person = await _persons.GetByIdAsync(request.PersonId, cancellationToken);
            if (person is null)
                return OperationResult<int>.Failure("الشخص غير موجود.", ErrorType.NotFound);
            if (!person.IsActive)
                return OperationResult<int>.Failure("لا يمكن إنشاء ملف لشخص معطّل — فعّله أولاً.", ErrorType.BusinessRule);

            // الفهرس المفلتر يضمن القاعدة، وهذا الفحص يعطي الرسالة النظيفة (D-22)
            if (await _teachers.AnyActiveForPersonAsync(request.PersonId, cancellationToken))
                return OperationResult<int>.Failure("لهذا الشخص ملف أستاذ فعّال بالفعل.", ErrorType.Conflict);

            var utcNow = _clock.UtcNow;
            var teacher = Teacher.Create(request.PersonId, request.Specialty, request.Notes,
                utcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
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
            _logger.LogError(ex, "Failed to create teacher file for person {PersonId}", request.PersonId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء ملف الأستاذ.", ErrorType.Unexpected);
        }
    }
}