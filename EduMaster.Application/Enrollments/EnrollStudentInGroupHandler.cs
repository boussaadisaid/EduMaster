using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>
/// الإلحاق بالفوج — الحُراس (D-54/D-59/D-79): فوج فعّال · طالب فعّال (ملف وشخص) · تسجيل سنوي نشط مطابق
/// (نفس السنة والمستوى، والشعبة ضمن شعب الفوج إن قُيّد) · لا نشط مكرر · السعة صارمة · والسعر يُنسخ من جدول الأسعار (D-50/D-77)
/// </summary>
public sealed record EnrollStudentInGroupRequest(
    int ClassGroupId,
    int StudentId,
    long? AgreedUnitPriceCentimes,   // null = خذ سعر الجدول كما هو
    string? DiscountNote);

public sealed class EnrollStudentInGroupHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAnnualEnrollmentRepository _annualEnrollments;
    private readonly IStudentRepository _students;
    private readonly IPersonRepository _persons;
    private readonly ISubjectPriceRepository _prices;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnrollStudentInGroupHandler> _logger;

    public EnrollStudentInGroupHandler(IClassGroupEnrollmentRepository groupEnrollments, IClassGroupRepository classGroups,
        IAnnualEnrollmentRepository annualEnrollments, IStudentRepository students, IPersonRepository persons,
        ISubjectPriceRepository prices, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<EnrollStudentInGroupHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _classGroups = classGroups;
        _annualEnrollments = annualEnrollments;
        _students = students;
        _persons = persons;
        _prices = prices;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(EnrollStudentInGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AgreedUnitPriceCentimes < 0)
            return OperationResult<int>.Failure("السعر لا يمكن أن يكون سالباً.", ErrorType.Validation);

        try
        {
            var group = await _classGroups.GetByIdAsync(request.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult<int>.Failure("الفوج غير موجود.", ErrorType.NotFound);
            if (!group.IsActive)
                return OperationResult<int>.Failure("الفوج معطّل — لا يقبل تسجيلات جديدة.", ErrorType.BusinessRule);

            var student = await _students.GetByIdAsync(request.StudentId, cancellationToken);
            if (student is null)
                return OperationResult<int>.Failure("الطالب المحدد غير موجود.", ErrorType.Validation);

            var person = await _persons.GetByIdAsync(student.PersonId, cancellationToken);
            if (person is null || !person.IsActive)
                return OperationResult<int>.Failure("لا يمكن تسجيل طالب شخصه معطّل — فعّله من شاشة الأشخاص أولاً.", ErrorType.BusinessRule);

            // D-54: تسجيل سنوي نشط في سنة الفوج — الفهرس المفلتر يضمن صفاً واحداً كحد أقصى
            var annual = await _annualEnrollments.GetActiveForStudentInYearAsync(request.StudentId, group.AcademicYearId, cancellationToken);
            if (annual is null)
                return OperationResult<int>.Failure("ليس للطالب تسجيل سنوي نشط في سنة هذا الفوج — أنشئه أولاً من لوحة التسجيلات.", ErrorType.BusinessRule);

            if (annual.LevelId != group.LevelId)
                return OperationResult<int>.Failure("مستوى الطالب في تسجيله السنوي لا يطابق مستوى الفوج — عدّل تسجيله السنوي أو اختر فوجاً آخر.", ErrorType.BusinessRule);

            // D-54/D-59: فوج مقيّد بشعب ← شعبة الطالب يجب أن تكون ضمنها (وبلا شعبة ← ممنوع)
            var groupStreamIds = await _classGroups.GetStreamIdsAsync(group.Id, cancellationToken);
            if (groupStreamIds.Count > 0)
            {
                if (annual.StreamId is null)
                    return OperationResult<int>.Failure("هذا الفوج مقيّد بشعب محددة والطالب بلا شعبة في تسجيله السنوي — سجّل شعبته أولاً.", ErrorType.BusinessRule);
                if (!groupStreamIds.Contains(annual.StreamId.Value))
                    return OperationResult<int>.Failure("شعبة الطالب في تسجيله السنوي ليست ضمن شعب هذا الفوج.", ErrorType.BusinessRule);
            }

            // فرادة النشط الودية قبل الاصطدام بالفهرس المفلتر (D-22/D-53)
            if (await _groupEnrollments.AnyActiveForStudentInGroupAsync(group.Id, request.StudentId, cancellationToken))
                return OperationResult<int>.Failure("الطالب مسجَّل في هذا الفوج بالفعل.", ErrorType.Conflict);

            // D-79: السعة صارمة
            if (group.Capacity is not null
                && await _groupEnrollments.CountActiveInGroupAsync(group.Id, cancellationToken) >= group.Capacity.Value)
                return OperationResult<int>.Failure($"الفوج ممتلئ (سعته {group.Capacity.Value}) — ارفع السعة من محرر الفوج أو اختر فوجاً آخر.", ErrorType.BusinessRule);

            // D-77: الاقتراح من جدول الأسعار · الغياب ← إدخال يدوي إلزامي
            var snapshotCentimes = await _prices.TryGetPriceAsync(group.AcademicYearId, group.LevelId, group.SubjectId, cancellationToken);
            if (snapshotCentimes is null && request.AgreedUnitPriceCentimes is null)
                return OperationResult<int>.Failure("لا سعر في جدول الأسعار لهذه المادة في هذا المستوى لهذه السنة — أدخل السعر يدوياً.", ErrorType.Validation);

            var agreedCentimes = request.AgreedUnitPriceCentimes ?? snapshotCentimes!.Value;
            if (agreedCentimes < 0)
                return OperationResult<int>.Failure("السعر لا يمكن أن يكون سالباً.", ErrorType.Validation);

            var enrollment = Domain.Enrollments.ClassGroupEnrollment.Create(
                group.Id, request.StudentId, annual.Id,
                snapshotCentimes ?? agreedCentimes, agreedCentimes, request.DiscountNote,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _groupEnrollments.AddAsync(enrollment, cancellationToken);
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
            _logger.LogError(ex, "Failed to enroll student {StudentId} in class group {ClassGroupId}", request.StudentId, request.ClassGroupId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء الإلحاق بالفوج.", ErrorType.Unexpected);
        }
    }
}