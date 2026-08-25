using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Payroll;

/// <summary>DTOs الصرف والأرصدة (F5 — الشريحة 5.3) — النصوص العربية للعرض تُحسب هنا كعادة DTOs الأجور.</summary>

/// <summary>مجموع معتمد لمستفيد (Σ سطوره في الكشوف المعتمدة فقط — المسودات لا تصنع ديناً).</summary>
public sealed record PayeeApprovedTotal(PayeeKind PayeeKind, int PayeeId, long TotalCentimes);

/// <summary>مجموع مصروف لمستفيد عبر التاريخ (Σ إيصالاته — الصافي قد يسوّد = سلفة زائدة).</summary>
public sealed record PayeePayoutTotal(PayeeKind PayeeKind, int PayeeId, long TotalCentimes);

/// <summary>صف الرصيد الجاري لمستفيد (تبويب «الأرصدة»): البقية = معتمد − مصروف — سالبها = سلفة قائمة (أحمر غامق — روح D-98).</summary>
public sealed record PayeeBalanceItem(PayeeKind PayeeKind, int PayeeId, string PayeeName, long ApprovedCentimes, long PaidCentimes)
{
    public long BalanceCentimes => ApprovedCentimes - PaidCentimes;
    public bool IsNegativeBalance => BalanceCentimes < 0;
    public string PayeeKindText => PayeeKind == PayeeKind.Teacher ? "أستاذ" : "موظف";
}

/// <summary>صف إيصال صرف في سجل المستفيد — السالب = قيد تصحيح (س-5).</summary>
public sealed record PayoutItem(int Id, int ReceiptNo, long AmountCentimes, string? Note, int? PayrollRunId, DateTime CreatedAtUtc)
{
    public bool IsCorrection => AmountCentimes < 0;
}