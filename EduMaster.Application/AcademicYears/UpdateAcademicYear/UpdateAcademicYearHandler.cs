using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;



namespace EduMaster.Application.AcademicYears.UpdateAcademicYear
{
    public sealed class UpdateAcademicYearHandler
    {
        private readonly IAcademicYearRepository _years;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateAcademicYearHandler> _logger;

        public UpdateAcademicYearHandler(
        IAcademicYearRepository years,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<UpdateAcademicYearHandler> logger)
        {
            _years = years;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OperationResult> ExecuteAsync(UpdateAcademicYearRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Name))
                return OperationResult.Failure("أدخل اسم السنة الدراسية.", ErrorType.Validation);

            try
            {
                var year = await _years.GetByIdAsync(request.Id, cancellationToken);
                if (year is null)
                    return OperationResult.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);

                var name = new YearName(request.Name);

                // excludeId = year.Id كي لا تتعارض السنة مع ذاتها
                if (await _years.AnyWithNameAsync(name.Value, excludeId: year.Id, cancellationToken))
                    return OperationResult.Failure($"توجد سنة دراسية بالاسم «{name}» مسبقاً.", ErrorType.Conflict);

                if (await _years.AnyOverlappingAsync(request.StartDate, request.EndDate, excludeId: year.Id, cancellationToken))
                    return OperationResult.Failure("الفترة المدخلة تتداخل مع سنة دراسية موجودة.", ErrorType.Conflict);

                // سلوك الكيان يرمي DomainException عند كسر قواعد التواريخ/المطابقة
                year.Update(name, request.StartDate, request.EndDate, _clock.UtcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _years.UpdateAsync(year, cancellationToken);
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
                _logger.LogError(ex, "Failed to update academic year {AcademicYearId}", request.Id);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل السنة الدراسية.", ErrorType.Unexpected);
            }
        }
    }
}