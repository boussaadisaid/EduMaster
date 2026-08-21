using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Students;
using Microsoft.Extensions.Logging;




namespace EduMaster.Application.Students
{
    public sealed record CreateStudentFileRequest(int PersonId, int? GuardianPersonId, StudentCategory Category, string? Notes);

    public sealed class CreateStudentFileHandler
    {
        // الاعتماديات: IPersonRepository + IStudentRepository + IClock + ICurrentUserService + IUnitOfWork + ILogger
        private readonly IPersonRepository _persons;
        private readonly IStudentRepository _students;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateStudentFileHandler> _logger;

        public CreateStudentFileHandler(
            IPersonRepository persons,
            IStudentRepository students,
            IClock clock,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork,
            ILogger<CreateStudentFileHandler> logger)
        {
            _persons = persons;
            _students = students;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public async Task<OperationResult<int>> ExecuteAsync(CreateStudentFileRequest request, CancellationToken cancellationToken = default)
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
                if (await _students.AnyActiveForPersonAsync(request.PersonId, cancellationToken))
                    return OperationResult<int>.Failure("لهذا الشخص ملف طالب فعّال بالفعل.", ErrorType.Conflict);

                if (request.GuardianPersonId is not null)
                {
                    var guardian = await _persons.GetByIdAsync(request.GuardianPersonId.Value, cancellationToken);
                    if (guardian is null)
                        return OperationResult<int>.Failure("ولي الأمر المحدد غير موجود.", ErrorType.Validation);
                    if (!guardian.IsActive)
                        return OperationResult<int>.Failure("ولي الأمر المحدد معطّل — فعّله أولاً.", ErrorType.BusinessRule);
                }

                var utcNow = _clock.UtcNow;
                var student = Student.Create(request.PersonId, request.GuardianPersonId, request.Category,
                    request.Notes, utcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
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
                _logger.LogError(ex, "Failed to create student file for person {PersonId}", request.PersonId);
                return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء ملف الطالب.", ErrorType.Unexpected);
            }
        }
    }
}
