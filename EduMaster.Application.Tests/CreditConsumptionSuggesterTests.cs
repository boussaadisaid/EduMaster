using EduMaster.Application.Billing;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>مقترِح استهلاك الزائدة (6.6 — ز-1، سداد وعد D-107) — نقي عددي (مال ⇒ قاعدة 4.0): السقوف الثلاثة + الأقدم أولاً + التخطّي الدفاعي</summary>
public class CreditConsumptionSuggesterTests
{
    private static CreditConsumptionSuggester.ReceiptFree R(int id, long free) => new(id, free);
    private static CreditConsumptionSuggester.ChargeOpen C(int id, long remaining) => new(id, remaining);

    [Fact]
    public void ZeroCredit_SuggestsNothing()
        => Assert.Empty(CreditConsumptionSuggester.Suggest(0, new[] { R(1, 1000) }, new[] { C(10, 500) }));

    [Fact]
    public void NegativeCredit_SuggestsNothing()
        => Assert.Empty(CreditConsumptionSuggester.Suggest(-500, new[] { R(1, 1000) }, new[] { C(10, 500) }));

    [Fact]
    public void NoCharges_SuggestsNothing()
        => Assert.Empty(CreditConsumptionSuggester.Suggest(1000, new[] { R(1, 1000) }, new List<CreditConsumptionSuggester.ChargeOpen>()));

    [Fact]
    public void NoReceipts_SuggestsNothing()
        => Assert.Empty(CreditConsumptionSuggester.Suggest(1000, new List<CreditConsumptionSuggester.ReceiptFree>(), new[] { C(10, 500) }));

    [Fact]
    public void SingleReceiptSingleCharge_CapsAtChargeRemaining()
    {
        var result = CreditConsumptionSuggester.Suggest(100000, new[] { R(5, 80000) }, new[] { C(20, 30000) });

        var line = Assert.Single(result);
        Assert.Equal((5, 20, 30000L), (line.PaymentId, line.ChargeId, line.AmountCentimes));
    }

    [Fact]
    public void SpillsToNextReceipt_WhenFirstExhausted()
    {
        var result = CreditConsumptionSuggester.Suggest(100000, new[] { R(5, 30000), R(8, 90000) }, new[] { C(20, 50000) });

        Assert.Equal(2, result.Count);
        Assert.Equal((5, 20, 30000L), (result[0].PaymentId, result[0].ChargeId, result[0].AmountCentimes));
        Assert.Equal((8, 20, 20000L), (result[1].PaymentId, result[1].ChargeId, result[1].AmountCentimes));
    }

    [Fact]
    public void CreditCap_BelowReceiptFree_GuardsRefunds()   // الصرف ينقص الإجمالي ولا يُربط بإيصال — سقف الإجمالي حارسه
    {
        var result = CreditConsumptionSuggester.Suggest(25000, new[] { R(5, 70000) }, new[] { C(20, 90000) });

        var line = Assert.Single(result);
        Assert.Equal((5, 20, 25000L), (line.PaymentId, line.ChargeId, line.AmountCentimes));
    }

    [Fact]
    public void MultipleCharges_OldestFirstInOrder()
    {
        var result = CreditConsumptionSuggester.Suggest(100000, new[] { R(5, 70000) }, new[] { C(20, 40000), C(21, 50000) });

        Assert.Equal(2, result.Count);
        Assert.Equal((5, 20, 40000L), (result[0].PaymentId, result[0].ChargeId, result[0].AmountCentimes));
        Assert.Equal((5, 21, 30000L), (result[1].PaymentId, result[1].ChargeId, result[1].AmountCentimes));
    }

    [Fact]
    public void ZeroFreeReceipt_IsSkipped()   // دفاع — المستودع لا يعيدها، لكن المصفف لا يعلق عليها
    {
        var result = CreditConsumptionSuggester.Suggest(10000, new[] { R(5, 0), R(8, 10000) }, new[] { C(20, 5000) });

        var line = Assert.Single(result);
        Assert.Equal((8, 20, 5000L), (line.PaymentId, line.ChargeId, line.AmountCentimes));
    }

    [Fact]
    public void CombinedCase_NeverBreachesAnyCap()
    {
        var result = CreditConsumptionSuggester.Suggest(60000,
            new[] { R(5, 50000), R(8, 50000) }, new[] { C(20, 30000), C(21, 40000) });

        Assert.Equal(3, result.Count);
        Assert.Equal((5, 20, 30000L), (result[0].PaymentId, result[0].ChargeId, result[0].AmountCentimes));
        Assert.Equal((5, 21, 20000L), (result[1].PaymentId, result[1].ChargeId, result[1].AmountCentimes));
        Assert.Equal((8, 21, 10000L), (result[2].PaymentId, result[2].ChargeId, result[2].AmountCentimes));
        Assert.Equal(60000L, result.Sum(s => s.AmountCentimes));   // = الزائدة تماماً ولا سنتيم فوقها
    }
}
