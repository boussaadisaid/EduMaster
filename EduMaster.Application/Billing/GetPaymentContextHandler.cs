using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>سياق ديالوغ القبض بقراءة واحدة: المستحقات المفتوحة (الأقدم أولاً) + الزائدة الدائنة (D-107) + معرّف ولي الطالب المسجَّل (D-36/D-104) — بلا معاملة</summary>
public sealed class GetPaymentContextHandler
{
    private readonly IChargeRepository _charges;
    private readonly IPaymentRepository _payments;
    private readonly IStudentRepository _students;
    private readonly IAcademicYearRepository _academicYears;
    private readonly ILogger<GetPaymentContextHandler> _logger;

    public GetPaymentContextHandler(IChargeRepository charges, IPaymentRepository payments, IStudentRepository students,
        IAcademicYearRepository academicYears, ILogger<GetPaymentContextHandler> logger)
    {
        _charges = charges;
        _payments = payments;
        _students = students;
        _academicYears = academicYears;
        _logger = logger;
    }

    public async Task<OperationResult<PaymentContextItem>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentAcademicYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentAcademicYear is null)
                return OperationResult<PaymentContextItem>.Failure(
                    "لا توجد سنة دراسية حالية محددة.", ErrorType.BusinessRule);

            var openCharges = (await _charges.GetOpenForStudentAsync(studentId, cancellationToken)).ToList();
            var unallocated = await _payments.GetUnallocatedForStudentAsync(studentId, cancellationToken);
            var student = await _students.GetByIdAsync(studentId, cancellationToken);

            var current = openCharges
                .Where(c => c.AcademicYearId == currentAcademicYear.Id)
                .ToList();
            var otherYears = openCharges
                .Where(c => c.AcademicYearId != currentAcademicYear.Id)
                .ToList();

            return OperationResult<PaymentContextItem>.Success(
                new PaymentContextItem(openCharges, unallocated, student?.GuardianPersonId)
                {
                    CurrentYearOpenCharges = current,
                    OtherYearsOpenCharges = otherYears,
                    CurrentAcademicYearId = currentAcademicYear.Id,
                    CurrentAcademicYearName = currentAcademicYear.Name.ToString()
                });
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load payment context for student {StudentId}", studentId);
            return OperationResult<PaymentContextItem>.Failure("حدث خطأ غير متوقع أثناء تحميل سياق القبض.", ErrorType.Unexpected);
        }
    }
}