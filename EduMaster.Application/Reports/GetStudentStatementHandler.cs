using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Reports;

/// <summary>كشف حساب طالب (6.1 — D-127): تجميعي من قراءات قائمة + قراءة التقارير الجديدة — بلا معاملة، يرمي الإلغاء (D-64)
/// · وصف سطور التخصيص يُركَّب من قائمة المستحقات نفسها — لا تكرار لتعبير SQL (تصويب ت-أ)</summary>
public sealed class GetStudentStatementHandler
{
    private readonly IStudentRepository _students;
    private readonly IChargeRepository _charges;
    private readonly IPaymentRepository _payments;
    private readonly IReportRepository _reports;
    private readonly IAcademicYearRepository _academicYears;
    private readonly ILogger<GetStudentStatementHandler> _logger;

    public GetStudentStatementHandler(IStudentRepository students, IChargeRepository charges, IPaymentRepository payments,
        IReportRepository reports, IAcademicYearRepository academicYears, ILogger<GetStudentStatementHandler> logger)
    {
        _students = students;
        _charges = charges;
        _payments = payments;
        _reports = reports;
        _academicYears = academicYears;
        _logger = logger;
    }

    public Task<OperationResult<StudentStatementItem>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
        => ExecuteInternalAsync(studentId, null, cancellationToken);

    public Task<OperationResult<StudentStatementItem>> ExecuteForAcademicYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default)
        => ExecuteInternalAsync(studentId, academicYearId, cancellationToken);

    private async Task<OperationResult<StudentStatementItem>> ExecuteInternalAsync(
        int studentId, int? academicYearId, CancellationToken cancellationToken)
    {
        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student is null)
                return OperationResult<StudentStatementItem>.Failure("الطالب غير موجود.", ErrorType.NotFound);

            string? academicYearName = null;
            if (academicYearId.HasValue)
            {
                var academicYear = await _academicYears.GetByIdAsync(academicYearId.Value, cancellationToken);
                if (academicYear is null)
                    return OperationResult<StudentStatementItem>.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);
                academicYearName = academicYear.Name.Value;
            }

            var allCharges = (await _charges.GetForStudentAsync(studentId, cancellationToken)).ToList();
            var read = await _reports.GetPaymentsWithAllocationsForStudentAsync(studentId, cancellationToken);
            var credit = await _payments.GetUnallocatedForStudentAsync(studentId, cancellationToken);

            var charges = academicYearId.HasValue
                ? allCharges.Where(c => c.AcademicYearId == academicYearId.Value).ToList()
                : allCharges;

            var chargeIdsInScope = academicYearId.HasValue
                ? charges.Select(c => c.Id).ToHashSet()
                : allCharges.Select(c => c.Id).ToHashSet();

            var descriptionByChargeId = allCharges.ToDictionary(c => c.Id, c => c.SourceDescription);

            var payments = read.Payments
                .Select(p =>
                {
                    var paymentAllocations = read.Allocations.Where(a => a.PaymentId == p.Id).ToList();
                    var scopedAllocations = academicYearId.HasValue
                        ? paymentAllocations.Where(a => chargeIdsInScope.Contains(a.ChargeId)).ToList()
                        : paymentAllocations;

                    var scopedAmount = scopedAllocations.Sum(a => a.AmountCentimes);
                    if (academicYearId.HasValue && p.Kind != PaymentKind.Receipt)
                        scopedAmount = 0;

                    var displayedAllocations = scopedAllocations
                        .Select(a => new StudentPaymentAllocationLine(
                            a.ChargeId,
                            descriptionByChargeId.TryGetValue(a.ChargeId, out var description) ? description : "مستحق غير ظاهر في الكشف",
                            a.AmountCentimes))
                        .ToList();

                    return new
                    {
                        Payment = new StudentPaymentLine(
                            p.Id, p.ReceiptNo, p.Kind, p.PayerName, p.AmountCentimes, p.PaidOn, p.Note, p.AllocatedCentimes,
                            displayedAllocations)
                        {
                            AllocatedToSelectedAcademicYearCentimes = scopedAmount
                        },
                        ScopedAmount = scopedAmount
                    };
                })
                .Where(x => !academicYearId.HasValue || (x.Payment.Kind == PaymentKind.Receipt && x.ScopedAmount > 0))
                .Select(x => x.Payment)
                .ToList();

            return OperationResult<StudentStatementItem>.Success(new StudentStatementItem(charges, payments, credit)
            {
                IsAcademicYearScoped = academicYearId.HasValue,
                AcademicYearId = academicYearId,
                AcademicYearName = academicYearName
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build account statement for student {StudentId} (AcademicYearId: {AcademicYearId})", studentId, academicYearId);
            return OperationResult<StudentStatementItem>.Failure("حدث خطأ غير متوقع أثناء إعداد كشف الحساب.", ErrorType.Unexpected);
        }
    }
}
