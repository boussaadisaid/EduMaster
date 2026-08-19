using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.AcademicYears.DeactivateAcademicYear;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;


namespace EduMaster.Application.AcademicYears.ActivateAcademicYear
{
    public sealed class ActivateAcademicYearHandler
    {
        private readonly IAcademicYearRepository _years;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ActivateAcademicYearHandler> _logger;

        public ActivateAcademicYearHandler(
            IAcademicYearRepository years,
            IClock clock,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork,
            ILogger<ActivateAcademicYearHandler> logger)
        {
            _years = years ?? throw new ArgumentNullException(nameof(years));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationResult> ExecuteAsync(ActivateAcademicYearRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var year = await _years.GetByIdAsync(request.Id, cancellationToken);
                if (year is null)
                    return OperationResult.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);

                if (year.IsActive)
                    return OperationResult.Success();   // فعّالة أصلاً

                year.Activate(_clock.UtcNow, _currentUser.UserAccountId);

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
                _logger.LogError(ex, "Failed to activate academic year {AcademicYearId}", request.Id);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل السنة الدراسية.", ErrorType.Unexpected);
            }
        }
    }
}
