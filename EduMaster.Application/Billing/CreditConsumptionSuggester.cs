namespace EduMaster.Application.Billing;

/// <summary>
/// مقترِح استهلاك الزائدة الدائنة (6.6 — ز-1، سداد وعد D-107) — دالة نقية مختبَرة عددياً (مال ⇒ قاعدة 4.0):
/// تسيل الزائدة على المستحقات المفتوحة (الأقدم أولاً — مرآة D-106) من الإيصالات الحرة (الأقدم أولاً)،
/// بثلاثة سقوف لا تُخترق: حرية كل إيصال (مبلغه − Σ تخصيصاته) · متبقّي كل مستحق · إجمالي الزائدة على الحساب
/// (Σقبض − Σمخصوص − Σصرف — الصرف غير مربوط بإيصال، فسقف الإجمالي حارسه الموثّق).
/// </summary>
public static class CreditConsumptionSuggester
{
    public sealed record ReceiptFree(int PaymentId, long FreeCentimes);
    public sealed record ChargeOpen(int ChargeId, long RemainingCentimes);
    public sealed record AllocationSuggestion(int PaymentId, int ChargeId, long AmountCentimes);

    public static IReadOnlyList<AllocationSuggestion> Suggest(
        long creditCentimes,
        IReadOnlyList<ReceiptFree> receiptsOldestFirst,
        IReadOnlyList<ChargeOpen> openChargesOldestFirst)
    {
        ArgumentNullException.ThrowIfNull(receiptsOldestFirst);
        ArgumentNullException.ThrowIfNull(openChargesOldestFirst);

        var result = new List<AllocationSuggestion>();
        if (creditCentimes <= 0)
            return result;

        var creditLeft = creditCentimes;
        var receiptIndex = 0;
        var receiptFreeLeft = receiptsOldestFirst.Count > 0 ? receiptsOldestFirst[0].FreeCentimes : 0L;

        foreach (var charge in openChargesOldestFirst)
        {
            var chargeLeft = charge.RemainingCentimes;
            while (chargeLeft > 0 && creditLeft > 0)
            {
                // تخطَّ الإيصالات المستهلكة/الصفريّة (دفاع — المستودع لا يعيد إلا حرّية > 0)
                while (receiptIndex < receiptsOldestFirst.Count && receiptFreeLeft <= 0)
                {
                    receiptIndex++;
                    if (receiptIndex < receiptsOldestFirst.Count)
                        receiptFreeLeft = receiptsOldestFirst[receiptIndex].FreeCentimes;
                }
                if (receiptIndex >= receiptsOldestFirst.Count)
                    return result;

                var amount = Math.Min(Math.Min(receiptFreeLeft, chargeLeft), creditLeft);
                result.Add(new AllocationSuggestion(receiptsOldestFirst[receiptIndex].PaymentId, charge.ChargeId, amount));
                receiptFreeLeft -= amount;
                chargeLeft -= amount;
                creditLeft -= amount;
            }

            if (creditLeft <= 0)
                break;
        }

        return result;
    }
}
