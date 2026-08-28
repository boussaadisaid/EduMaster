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

    /// <summary>إيصالات الطالب الحرة (قبض بحريّة > 0) — الأقدم أولاً · لاستهلاك الزائدة (6.6 — ز-1): الصرف غير مربوط بإيصال فسقف الإجمالي حارسه الموثّق</summary>
    Task<IReadOnlyList<UnallocatedReceiptRaw>> GetUnallocatedReceiptsForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>بطاقة إيصال لعكسه (6.6-ع-4) — بعلم «عُكس من قبل» باتفاق وسم الملاحظة المولَّد</summary>
    Task<ReceiptReversalInfoRaw?> GetReceiptReversalInfoAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>فكّ تخصيصات إيصال (6.6-ع-ب2): الجدول فريد الزوج ومشروط الموجب — الإزالة هي فكّ التخصيص المصمَّمة · الوثيقة (الإيصال) لا تُحذف أبداً (D-109) والحدث موثّق بإيصال العكس الموسوم</summary>
    Task DeleteAllocationsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>فكّ تخصيصات مستحق (6.6-ع-ب2): نفس القاعدة — إلغاؤه يحرر ماله إلى الزائدة، والحدث موثّق بحالته وسببه</summary>
    Task DeleteAllocationsForChargeAsync(int chargeId, CancellationToken cancellationToken = default);

    /// <summary>سجل الفترة مسطّحاً (قبض + صرف — 4.3): الأحدث أولاً بمخصوص كل دفعة</summary>
    Task<IEnumerable<PaymentListItem>> GetForPeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>حارس D-109: ملف عليه مدفوعات لا يُزال</summary>
    Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default);
}