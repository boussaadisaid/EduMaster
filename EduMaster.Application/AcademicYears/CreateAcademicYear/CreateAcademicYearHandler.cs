using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.AcademicYears.CreateAcademicYear;

public sealed class CreateAcademicYearHandler
{
    private readonly IAcademicYearRepository _years;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAcademicYearHandler> _logger;

    public CreateAcademicYearHandler(
        IAcademicYearRepository years,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateAcademicYearHandler> logger)
    {
        _years = years;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(CreateAcademicYearRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
            return OperationResult<int>.Failure("أدخل اسم السنة الدراسية.", ErrorType.Validation);

        if (request.RegistrationFeeCentimes < 0)
            return OperationResult<int>.Failure("حقوق التسجيل لا يمكن أن تكون سالبة.", ErrorType.Validation);

        try
        {
            // ① بناء الكيان أولاً — قواعد الصيغة/التواريخ/المطابقة/التتابع تعيش في الدومين وترمي DomainException عربية
            var name = new YearName(request.Name);
            var year = AcademicYear.Create(name, request.StartDate, request.EndDate, request.RegistrationFeeCentimes,
                _clock.UtcNow, _currentUser.UserAccountId);

            // ② فحوصات التعارض (قراءة) قبل فتح المعاملة — رسالة نظيفة بدل اصطدام قيود القاعدة (D-22/D-24)
            if (await _years.AnyWithNameAsync(name.Value, excludeId: 0, cancellationToken))
                return OperationResult<int>.Failure($"توجد سنة دراسية بالاسم «{name}» مسبقاً.", ErrorType.Conflict);

            if (await _years.AnyOverlappingAsync(request.StartDate, request.EndDate, excludeId: 0, cancellationToken))
                return OperationResult<int>.Failure("الفترة المدخلة تتداخل مع سنة دراسية موجودة.", ErrorType.Conflict);

            // ③ معاملة حول الكتابة — Commit أو Rollback على كل مسار
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _years.AddAsync(year, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(year.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);   // آمنة حتى بلا معاملة مفتوحة
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create academic year {YearName}", request.Name);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء السنة الدراسية.", ErrorType.Unexpected);
        }
    }
}