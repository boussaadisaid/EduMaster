using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.AcademicYears.SetCurrentAcademicYear
{
    public sealed class SetCurrentAcademicYearHandler
    {
        private readonly IAcademicYearRepository _years;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SetCurrentAcademicYearHandler> _logger;

        public SetCurrentAcademicYearHandler(
            IAcademicYearRepository repository,
            IUnitOfWork unitOfWork,
            IClock clock,
            ICurrentUserService currentUser,
            ILogger<SetCurrentAcademicYearHandler> logger)
        {
            _years = repository;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<OperationResult> ExecuteAsync(SetCurrentAcademicYearRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var target = await _years.GetByIdAsync(request.Id, cancellationToken);
                if (target is null)
                    return OperationResult.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);

                if (target.IsCurrent)
                    return OperationResult.Success();   // هي الحالية أصلاً — لا شيء يُفعل

                if (!target.IsActive)
                    return OperationResult.Failure("لا يمكن تعيين سنة معطّلة كحالية — فعّلها أولاً.", ErrorType.BusinessRule);

                var current = await _years.GetCurrentAcademicYearAsync(cancellationToken);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                // ⭐ الترتيب حاسم: الفهرس المفلتر يفحص كل UPDATE فورياً — إلغاء القديمة أولاً، ثم تعيين الجديدة
                if (current is not null)
                {
                    current.SetAsNotCurrent(_clock.UtcNow, _currentUser.UserAccountId);
                    await _years.UpdateAsync(current, cancellationToken);
                }

                target.SetAsCurrent(_clock.UtcNow, _currentUser.UserAccountId);
                await _years.UpdateAsync(target, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);
                return OperationResult.Success();
            }
            catch (DomainException dex)
            {
                // لو رمى حارس الكيان بعد إلغاء القديمة: الـRollback يعيدها — لا لحظة «بلا سنة حالية» ولا صفّان حاليّان
                await _unitOfWork.RollbackAsync(cancellationToken);
                return OperationResult.Failure(dex.Message, ErrorType.BusinessRule);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to set academic year {AcademicYearId} as current", request.Id);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعيين السنة الحالية.", ErrorType.Unexpected);
            }
        }



    }
}