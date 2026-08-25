using EduMaster.Application.Billing;
using EduMaster.Domain.Billing;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>الإيصالات وثائق: كتابة + قراءات مسطّحة (لا قراءة كيانية — العكس بإيصال صرف D-108)</summary>
public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    Task AddAllocationAsync(PaymentAllocation allocation, CancellationToken cancellationToken = default);

    /// <summary>الرقم التالي للإيصال — يُستدعى داخل معاملة التسجيل، والفهرس الفريد يحرسه (D-105)</summary>
    Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default);

    /// <summary>الزائدة الدائنة للطالب (D-107): Σقبض − Σمخصوص − Σصرف — تظهر «متاحاً» في قبضه القادم وتحرس الصرف (4.3)</summary>
    Task<long> GetUnallocatedForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>سجل الفترة مسطّحاً (قبض + صرف — 4.3): الأحدث أولاً بمخصوص كل دفعة</summary>
    Task<IEnumerable<PaymentListItem>> GetForPeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>حارس D-109: ملف عليه مدفوعات لا يُزال</summary>
    Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default);
}