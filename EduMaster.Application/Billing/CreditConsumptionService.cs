using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Billing;

namespace EduMaster.Application.Billing;

/// <summary>
/// استهلاك الزائدة الدائنة (6.6 — ز-1، سداد وعد D-107): يُستدعى قبل Commit داخل معاملة المتصل —
/// يقرأ زائدة الحساب (Σقبض − Σمخصوص − Σصرف) والمستحقات المفتوحة (الأقدم أولاً) والإيصالات الحرة (الأقدم أولاً)،
/// يقترح بالمصفف النقي المختبَر، ويكتب سطور التخصيص · المخصوص مشتق من Σ التخصيصات فلا صف مستحق يُلمس (إدراج تخصيصات فقط) ·
/// صامت في الكتابة ظاهر في القراءات (تنقص الزائدة في سياق القبض وتظهر التخصيصات في كشف الحساب) — حسمته دراسة 6.6.
/// </summary>
public sealed class CreditConsumptionService
{
    private readonly IPaymentRepository _payments;
    private readonly IChargeRepository _charges;

    public CreditConsumptionService(IPaymentRepository payments, IChargeRepository charges)
    {
        _payments = payments;
        _charges = charges;
    }

    /// <summary>يستهلك زائدة الطالب في مستحقاته المفتوحة — يعيد عدد سطور التخصيص المكتوبة · بلا معاملة خاصة: ما يُرمى في معاملة المتصل يتراجع معها</summary>
    public async Task<int> ConsumeForStudentAsync(int studentId, DateTime utcNow, int? userId, CancellationToken cancellationToken = default)
    {
        var credit = await _payments.GetUnallocatedForStudentAsync(studentId, cancellationToken);
        if (credit <= 0)
            return 0;

        var openCharges = (await _charges.GetOpenForStudentAsync(studentId, cancellationToken))
            .Select(c => new CreditConsumptionSuggester.ChargeOpen(c.Id, c.RemainingCentimes))
            .ToList();
        if (openCharges.Count == 0)
            return 0;

        var freeReceipts = (await _payments.GetUnallocatedReceiptsForStudentAsync(studentId, cancellationToken))
            .Select(r => new CreditConsumptionSuggester.ReceiptFree(r.PaymentId, r.FreeCentimes))
            .ToList();
        if (freeReceipts.Count == 0)
            return 0;   // دفاع: زائدة > 0 بلا إيصال حرّ مستحيلة رياضياً — ولا كتابة بلا مصدر

        var suggestions = CreditConsumptionSuggester.Suggest(credit, freeReceipts, openCharges);
        foreach (var suggestion in suggestions)
        {
            var allocation = PaymentAllocation.Create(
                suggestion.PaymentId, suggestion.ChargeId, suggestion.AmountCentimes, utcNow, userId);
            await _payments.AddAllocationAsync(allocation, cancellationToken);
        }

        return suggestions.Count;
    }
}
