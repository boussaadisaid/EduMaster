using EduMaster.Application.Billing;
using EduMaster.Domain.Enums;
using System;
using System.Linq;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>المقترِح التلقائي (D-106): الأقدم أولاً حتى ينفد المبلغ — والفائض زائدة (D-107)</summary>
public sealed class PaymentAllocationSuggesterTests
{
    private static OpenChargeItem Open(int id, long amount, long allocated, int daysAgo) =>
        new(id, ChargeKind.SessionBundle, $"مستحق {id}", amount, allocated, new DateTime(2026, 8, 23).AddDays(-daysAgo));

    [Fact]
    public void ExactFit_SingleCharge_FullCoverage()
    {
        var suggestion = PaymentAllocationSuggester.Suggest(new[] { Open(1, 100000, 0, 5) }, 100000);

        var line = Assert.Single(suggestion);
        Assert.Equal(1, line.ChargeId);
        Assert.Equal(100000, line.AmountCentimes);
    }

    [Fact]
    public void OldestFirst_RegardlessOfInputOrder()
    {
        var charges = new[] { Open(2, 50000, 0, 1), Open(1, 80000, 0, 10) };   // الأقدم (1) واردة ثانياً

        var suggestion = PaymentAllocationSuggester.Suggest(charges, 100000);

        Assert.Equal(new[] { 1, 2 }, suggestion.Select(s => s.ChargeId).ToArray());
        Assert.Equal(80000, suggestion[0].AmountCentimes);
        Assert.Equal(20000, suggestion[1].AmountCentimes);
    }

    [Fact]
    public void PartialAmount_StopsAtLastPartial()
    {
        var charges = new[] { Open(1, 100000, 0, 10), Open(2, 100000, 0, 5) };

        var suggestion = PaymentAllocationSuggester.Suggest(charges, 150000);

        Assert.Equal(150000, suggestion.Sum(s => s.AmountCentimes));
        Assert.Equal(50000, suggestion[1].AmountCentimes);   // الثاني جزئي
    }

    [Fact]
    public void RespectsAlreadyAllocated_RemainderOnly()
    {
        var charges = new[] { Open(1, 100000, 60000, 5) };   // متبقّيه 40000

        var suggestion = PaymentAllocationSuggester.Suggest(charges, 100000);

        var line = Assert.Single(suggestion);
        Assert.Equal(40000, line.AmountCentimes);            // والفائض 60000 زائدة دائنة (لا يُقترح)
    }

    [Fact]
    public void NoOpenCharges_EmptySuggestion()
    {
        Assert.Empty(PaymentAllocationSuggester.Suggest(Array.Empty<OpenChargeItem>(), 100000));
    }

    [Fact]
    public void ZeroAmount_EmptySuggestion()
    {
        Assert.Empty(PaymentAllocationSuggester.Suggest(new[] { Open(1, 100000, 0, 5) }, 0));
    }
}