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
    private readonly ILogger<GetPaymentContextHandler> _logger;

    public GetPaymentContextHandler(IChargeRepository charges, IPaymentRepository payments, IStudentRepository students,
        ILogger<GetPaymentContextHandler> logger)
    {
        _charges = charges;
        _payments = payments;
        _students = students;
        _logger = logger;
    }

    public async Task<OperationResult<PaymentContextItem>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var openCharges = await _charges.GetOpenForStudentAsync(studentId, cancellationToken);
            var unallocated = await _payments.GetUnallocatedForStudentAsync(studentId, cancellationToken);
            var student = await _students.GetByIdAsync(studentId, cancellationToken);   // الولي المسجَّل إن وُجد (اسمه عند الواجهة أصلاً)

            return OperationResult<PaymentContextItem>.Success(
                new PaymentContextItem(openCharges.ToList(), unallocated, student?.GuardianPersonId));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load payment context for student {StudentId}", studentId);
            return OperationResult<PaymentContextItem>.Failure("حدث خطأ غير متوقع أثناء تحميل سياق القبض.", ErrorType.Unexpected);
        }
    }
}