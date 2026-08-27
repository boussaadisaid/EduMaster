using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

public sealed record RegisterAnnualEnrollmentRequest(
    int StudentId,
    int AcademicYearId,
    int LevelId,
    int? StreamId,
    long AgreedRegistrationFeeCentimes,
    string? RegistrationFeeNote);

public sealed class RegisterAnnualEnrollmentHandler
{
    private readonly IAnnualEnrollmentRepository _enrollments;
    private readonly IStudentRepository _students;
    private readonly IPersonRepository _persons;
    private readonly IAcademicYearRepository _years;
    private readonly ILevelRepository _levels;
    private readonly IStreamRepository _streams;
    private readonly IChargeRepository _charges;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreditConsumptionService _creditConsumption;
    private readonly ILogger<RegisterAnnualEnrollmentHandler> _logger;

    public RegisterAnnualEnrollmentHandler(IAnnualEnrollmentRepository enrollments, IStudentRepository students,
        IPersonRepository persons, IAcademicYearRepository years, ILevelRepository levels, IStreamRepository streams,
        IChargeRepository charges,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork,
        CreditConsumptionService creditConsumption,
        ILogger<RegisterAnnualEnrollmentHandler> logger)
    {
        _enrollments = enrollments;
        _students = students;
        _persons = persons;
        _years = years;
        _levels = levels;
        _streams = streams;
        _charges = charges;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _creditConsumption = creditConsumption;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(RegisterAnnualEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AgreedRegistrationFeeCentimes < 0)
            return OperationResult<int>.Failure("حقوق التسجيل لا يمكن أن تكون سالبة.", ErrorType.Validation);

        try
        {
            // GetByIdAsync يستثني الملفات المحذوفة منطقياً (IsDeleted = 0)
            var student = await _students.GetByIdAsync(request.StudentId, cancellationToken);
            if (student is null)
                return OperationResult<int>.Failure("الطالب المحدد غير موجود.", ErrorType.Validation);

            var person = await _persons.GetByIdAsync(student.PersonId, cancellationToken);
            if (person is null || !person.IsActive)
                return OperationResult<int>.Failure("لا يمكن تسجيل طالب شخصه معطّل — فعّله من شاشة الأشخاص أولاً.", ErrorType.BusinessRule);

            // D-71: أي سنة فعّالة تصح (تجهيز مسبق) — المعطّلة ممنوعة
            var year = await _years.GetByIdAsync(request.AcademicYearId, cancellationToken);
            if (year is null)
                return OperationResult<int>.Failure("السنة الدراسية المحددة غير موجودة.", ErrorType.Validation);
            if (!year.IsActive)
                return OperationResult<int>.Failure("لا يمكن التسجيل في سنة معطّلة.", ErrorType.BusinessRule);

            var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
            if (level is null)
                return OperationResult<int>.Failure("المستوى المحدد غير موجود.", ErrorType.Validation);
            if (!level.IsActive)
                return OperationResult<int>.Failure("المستوى المحدد معطّل — فعّله أولاً.", ErrorType.BusinessRule);

            if (request.StreamId is not null)
            {
                // الشعبة تتبع المستوى وتكون فعّالة — والقاعدة تفرض التطابق أيضاً عبر الـFK المركّب (بروح D-28)
                var levelStreams = await _streams.GetByLevelIdAsync(request.LevelId, cancellationToken);
                var stream = levelStreams.FirstOrDefault(s => s.Id == request.StreamId.Value);
                if (stream is null)
                    return OperationResult<int>.Failure("الشعبة المحددة لا تتبع المستوى المختار.", ErrorType.Validation);
                if (!stream.IsActive)
                    return OperationResult<int>.Failure("الشعبة المحددة معطّلة — فعّلها أو اختر غيرها.", ErrorType.BusinessRule);
            }

            // فرادة النشط الودية قبل الاصطدام بالفهرس المفلتر (D-22/D-53)
            if (await _enrollments.AnyActiveForStudentInYearAsync(request.StudentId, request.AcademicYearId, cancellationToken))
                return OperationResult<int>.Failure("لهذا الطالب تسجيل سنوي نشط في هذه السنة بالفعل.", ErrorType.Conflict);

            // طابع واحد لكلتا الكتابتين — ذرّية اللحظة كذرّية المعاملة
            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            var enrollment = Domain.Enrollments.AnnualEnrollment.Create(
                request.StudentId, request.AcademicYearId, request.LevelId, request.StreamId,
                request.AgreedRegistrationFeeCentimes, request.RegistrationFeeNote,
                utcNow, userId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _enrollments.AddAsync(enrollment, cancellationToken);

            // D-103: مستحق الحقوق يتولد في نفس المعاملة (0 = إعفاء ← لا مستحق)
            if (request.AgreedRegistrationFeeCentimes > 0)
            {
                var charge = Domain.Billing.Charge.CreateForRegistrationFee(
                    enrollment.StudentId, enrollment.Id, request.AgreedRegistrationFeeCentimes, utcNow, userId);
                await _charges.AddAsync(charge, cancellationToken);
            }

            // 6.6 — ز-1: الزائدة الدائنة (إن وُجدت) تستهلَك فوراً في المستحق الجديد وسابقه المفتوح — سداد وعد D-107 داخل المعاملة ذاتها
            await _creditConsumption.ConsumeForStudentAsync(request.StudentId, utcNow, userId, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(enrollment.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to register annual enrollment for student {StudentId} in year {AcademicYearId}",
                request.StudentId, request.AcademicYearId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء التسجيل السنوي.", ErrorType.Unexpected);
        }
    }
}