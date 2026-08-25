using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>عقد مستودع إيصالات الصرف (5.3) — وثائق مالية: إضافة وقراءة فقط، لا تعديل ولا حذف أبداً (الخطأ يُقابل بقيد عكسي — س-5).</summary>
public interface IPayoutRepository
{
    Task AddAsync(Payout payout, CancellationToken cancellationToken = default);

    /// <summary>رقم الإيصال التالي (MAX+1) — يُستدعى داخل معاملة الـHandler والفريد يحرسه قاعدةً (مرآة D-105): تسلسل موحّد للفريقين بلا فجوات.</summary>
    Task<int> GetNextReceiptNoAsync(CancellationToken cancellationToken = default);

    /// <summary>مجاميع المصروف لكل مستفيد عبر التاريخ (الصافي قد يسوّد = سلفة زائدة) — طرف «المصروف» من الرصيد الجاري.</summary>
    Task<IReadOnlyList<PayeePayoutTotal>> GetTotalsByPayeeAsync(CancellationToken cancellationToken = default);

    /// <summary>إيصالات مستفيد واحد — الأحدث أولاً (لديالوغ الصرف).</summary>
    Task<IReadOnlyList<Payout>> GetForPayeeAsync(PayeeKind payeeKind, int payeeId, CancellationToken cancellationToken = default);
}