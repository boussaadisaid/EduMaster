namespace EduMaster.Application.Billing;

/// <summary>
/// مقترِح التخصيص التلقائي (D-106) — دالة نقية مختبَرة: الأقدم أولاً حتى ينفد المبلغ،
/// والفائض يبقى زائدة دائنة (D-107). اقتراح فقط — السيادة للمستخدم قبل الحفظ.
/// </summary>
public static class PaymentAllocationSuggester
{
    public static IReadOnlyList<SuggestedAllocation> Suggest(IEnumerable<OpenChargeItem> openCharges, long amountCentimes)
    {
        var result = new List<SuggestedAllocation>();
        var remaining = amountCentimes;

        foreach (var charge in openCharges.OrderBy(c => c.CreatedAtUtc))   // الأقدم أولاً
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(remaining, charge.RemainingCentimes);
            if (take <= 0)
                continue;

            result.Add(new SuggestedAllocation(charge.Id, take));
            remaining -= take;
        }

        return result;
    }
}