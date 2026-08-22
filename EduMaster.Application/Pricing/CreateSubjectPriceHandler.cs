using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Pricing;

public sealed record CreateSubjectPriceRequest(int AcademicYearId, int LevelId, int SubjectId, long UnitPriceCentimes);

public sealed class CreateSubjectPriceHandler
{
    private readonly ISubjectPriceRepository _prices;
    private readonly IAcademicYearRepository _years;
    private readonly ILevelRepository _levels;
    private readonly ISubjectRepository _subjects;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateSubjectPriceHandler> _logger;

    public CreateSubjectPriceHandler(ISubjectPriceRepository prices, IAcademicYearRepository years,
        ILevelRepository levels, ISubjectRepository subjects, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateSubjectPriceHandler> logger)
    {
        _prices = prices;
        _years = years;
        _levels = levels;
        _subjects = subjects;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateSubjectPriceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UnitPriceCentimes < 0)
            return OperationResult<int>.Failure("سعر الحصة لا يمكن أن يكون سالباً.", ErrorType.Validation);

        try
        {
            // الأسعار بيانات إعداد لا تشغيل: الوجود يكفي — لا شرط فعّالية (تسعير مسبق لسنة قادمة جائز)
            var year = await _years.GetByIdAsync(request.AcademicYearId, cancellationToken);
            if (year is null)
                return OperationResult<int>.Failure("السنة الدراسية المحددة غير موجودة.", ErrorType.Validation);

            var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
            if (level is null)
                return OperationResult<int>.Failure("المستوى المحدد غير موجود.", ErrorType.Validation);

            var subject = await _subjects.GetByIdAsync(request.SubjectId, cancellationToken);
            if (subject is null)
                return OperationResult<int>.Failure("المادة المحددة غير موجودة.", ErrorType.Validation);

            // فحص الفرادة الودي قبل الاصطدام بالقيد (D-22)
            if (await _prices.AnyExistsAsync(request.AcademicYearId, request.LevelId, request.SubjectId, null, cancellationToken))
                return OperationResult<int>.Failure("يوجد سعر لهذه المادة في هذا المستوى لهذه السنة بالفعل — عدّل الموجود.", ErrorType.Conflict);

            var price = Domain.Pricing.SubjectPrice.Create(
                request.AcademicYearId, request.LevelId, request.SubjectId, request.UnitPriceCentimes,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _prices.AddAsync(price, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(price.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create subject price for year {AcademicYearId}, level {LevelId}, subject {SubjectId}",
                request.AcademicYearId, request.LevelId, request.SubjectId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إضافة السعر.", ErrorType.Unexpected);
        }
    }
}