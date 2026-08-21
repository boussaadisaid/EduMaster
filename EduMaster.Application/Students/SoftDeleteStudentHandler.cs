using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;




namespace EduMaster.Application.Students
{
    public sealed record SoftDeleteStudentRequest(int StudentId);

    public sealed class SoftDeleteStudentHandler
    {
        // الاعتماديات: IStudentRepository + IClock + ICurrentUserService + IUnitOfWork + ILogger

        private readonly IStudentRepository _students;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SoftDeleteStudentHandler> _logger;

        public SoftDeleteStudentHandler(
            IStudentRepository students,
            IClock clock,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork,
            ILogger<SoftDeleteStudentHandler> logger)
        {
            _students = students;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }


        public async Task<OperationResult> ExecuteAsync(SoftDeleteStudentRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var student = await _students.GetByIdAsync(request.StudentId, cancellationToken);
                if (student is null)
                    return OperationResult.Failure("ملف الطالب غير موجود.", ErrorType.NotFound);

                // ح-7: الإزالة للأخطاء فقط — ملف عليه بيانات تشغيلية يبقى للأرشيف (يُفعَّل الفحص في F2)
                if (await _students.HasOperationalDataAsync(request.StudentId, cancellationToken))
                    return OperationResult.Failure("لا يمكن إزالة ملف عليه بيانات تشغيلية (تسجيلات…). يبقى للأرشيف — ويمكنك تعطيل الشخص بدلاً من ذلك.", ErrorType.BusinessRule);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _students.SoftDeleteAsync(request.StudentId, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Admin {AdminUserId} soft-deleted student file {StudentId}", _currentUser.UserAccountId, request.StudentId);

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to soft-delete student {StudentId}", request.StudentId);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء إزالة ملف الطالب.", ErrorType.Unexpected);
            }
        }
    }
}
