using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
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
    private readonly ILogger<GetStudentStatementHandler> _logger;

    public GetStudentStatementHandler(IStudentRepository students, IChargeRepository charges, IPaymentRepository payments,
        IReportRepository reports, ILogger<GetStudentStatementHandler> logger)
    {
        _students = students;
        _charges = charges;
        _payments = payments;
        _reports = reports;
        _logger = logger;
    }

    public async Task<OperationResult<StudentStatementItem>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student is null)
                return OperationResult<StudentStatementItem>.Failure("الطالب غير موجود.", ErrorType.NotFound);

            var charges = (await _charges.GetForStudentAsync(studentId, cancellationToken)).ToList();
            var read = await _reports.GetPaymentsWithAllocationsForStudentAsync(studentId, cancellationToken);
            var credit = await _payments.GetUnallocatedForStudentAsync(studentId, cancellationToken);

            // الوصف من المستحقات المحمَّلة نفسها — التخصيص لمستحق الطالب ذاته مضمون بالقيود، والنص الاحتياطي صمّام عرض فقط
            var descriptionByChargeId = charges.ToDictionary(c => c.Id, c => c.SourceDescription);

            var payments = read.Payments
                .Select(p => new StudentPaymentLine(
                    p.Id, p.ReceiptNo, p.Kind, p.PayerName, p.AmountCentimes, p.PaidOn, p.Note, p.AllocatedCentimes,
                    read.Allocations
                        .Where(a => a.PaymentId == p.Id)
                        .Select(a => new StudentPaymentAllocationLine(
                            a.ChargeId,
                            descriptionByChargeId.TryGetValue(a.ChargeId, out var description) ? description : "مستحق غير ظاهر في الكشف",
                            a.AmountCentimes))
                        .ToList()))
                .ToList();

            return OperationResult<StudentStatementItem>.Success(new StudentStatementItem(charges, payments, credit));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build account statement for student {StudentId}", studentId);
            return OperationResult<StudentStatementItem>.Failure("حدث خطأ غير متوقع أثناء إعداد كشف الحساب.", ErrorType.Unexpected);
        }
    }
}