using EduMaster.Application.Billing;
using EduMaster.Domain.Billing;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IChargeRepository
{
    Task AddAsync(Charge charge, CancellationToken cancellationToken = default);
    Task<Charge?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>مجموع تخصيصات مستحق (6.6-ع-3) — لحارس «لا تخفيض تحت المخصوص»، ولعكس الإلغاء في ع-ب</summary>
    Task<long> GetAllocatedForChargeAsync(int chargeId, CancellationToken cancellationToken = default);

    /// <summary>تحديث التسوية فقط (الحالة/المبلغ الحالي/السبب/الإلغاء/التدقيق) — النوع والمصدر والأصلي ثوابت (D-108)</summary>
    Task UpdateAsync(Charge charge, CancellationToken cancellationToken = default);

    /// <summary>مستحقات طالب مسطّحة (D-40) بوصف مصدر عربي والمخصوص — الأحدث أولاً</summary>
    Task<IEnumerable<StudentChargeItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>المفتوحة للتخصيص (D-106): فعّالة وبمتبقٍّ > 0 — الأقدم أولاً للاقتراح التلقائي</summary>
    Task<IEnumerable<OpenChargeItem>> GetOpenForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>شاشة المالية (4.3): من عليهم متبقٍّ > 0 — الأكبر أولاً · بحث مبسّط بالاسم/الهاتف (بلا تطبيع في V1)</summary>
    Task<IEnumerable<DebtorItem>> GetDebtorsAsync(string? searchTerm, CancellationToken cancellationToken = default);

    /// <summary>حارس D-109: ملف عليه أي مستحقات لا يُزال</summary>
    Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default);
}