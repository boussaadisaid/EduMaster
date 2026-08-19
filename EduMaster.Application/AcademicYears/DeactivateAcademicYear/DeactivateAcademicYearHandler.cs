using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.AcademicYears.DeactivateAcademicYear
{
    public sealed class DeactivateAcademicYearHandler
    {
        private readonly IAcademicYearRepository _years;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeactivateAcademicYearHandler> _logger;

        public DeactivateAcademicYearHandler(
            IAcademicYearRepository repository,
            IUnitOfWork unitOfWork,
            IClock clock,
            ICurrentUserService currentUser,
            ILogger<DeactivateAcademicYearHandler> logger)
        {
            _years = repository;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<OperationResult> ExecuteAsync(DeactivateAcademicYearRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var year = await _years.GetByIdAsync(request.Id, cancellationToken);
                if (year is null)
                    return OperationResult.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);

                if (!year.IsActive)
                    return OperationResult.Success();   // معطّلة أصلاً — لا كتابة بلا معنى

                if (year.IsCurrent)
                    return OperationResult.Failure("لا يمكن تعطيل السنة الحالية — عيّن سنة أخرى حالية أولاً.", ErrorType.BusinessRule);

                // حارس «البيانات التشغيلية» — يعمل من اليوم، وجسم فحصه يُملأ في F2
                if (await _years.HasOperationalDataAsync(year.Id, cancellationToken))
                    return OperationResult.Failure("لا يمكن تعطيل سنة دراسية عليها بيانات تشغيلية.", ErrorType.BusinessRule);

                year.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _years.UpdateAsync(year, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                return OperationResult.Success();
            }
            catch (DomainException dex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                return OperationResult.Failure(dex.Message, ErrorType.BusinessRule);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to deactivate academic year {AcademicYearId}", request.Id);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل السنة الدراسية.", ErrorType.Unexpected);
            }
        }
    }
}
