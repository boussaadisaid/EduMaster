using EduMaster.Domain.Enums;

namespace EduMaster.Application.Billing;

/// <summary>مستحق مفتوح في ديالوغ القبض — فعّال بمتبقٍّ > 0</summary>
public sealed record OpenChargeItem(
    int Id,
    ChargeKind Kind,
    string SourceDescription,
    long AmountCentimes,
    long AllocatedCentimes,
    DateTime CreatedAtUtc)
{
    public long RemainingCentimes => AmountCentimes - AllocatedCentimes;
    public string KindText => Kind == ChargeKind.RegistrationFee ? "حقوق تسجيل" : "حزمة حصص";
}

/// <summary>سطر اقتراح تلقائي (D-106)</summary>
public sealed record SuggestedAllocation(int ChargeId, long AmountCentimes);

/// <summary>سياق ديالوغ القبض بقراءة واحدة: المفتوحة + الزائدة الدائنة (D-107) + معرّف الولي المسجَّل إن وُجد (D-104/D-36)</summary>
public sealed record PaymentContextItem(IReadOnlyList<OpenChargeItem> OpenCharges, long UnallocatedCentimes, int? GuardianPersonId);

/// <summary>سطر تخصيص مدخل من الديالوغ بعد التعديل (السيادة للمستخدم — D-106)</summary>
public sealed record PaymentAllocationInput(int ChargeId, long AmountCentimes);

/// <summary>مدين في شاشة المالية (4.3): طالب عليه متبقٍّ > 0 — قراءة مسطّحة (D-40)</summary>
public sealed record DebtorItem(
    int StudentId,
    string FullName,
    string? Phone,
    int OpenChargesCount,
    long RemainingCentimes);

/// <summary>
/// سطر سجل المدفوعات (4.3) — قراءة مسطّحة (D-40).
/// PaidOn تُقرأ DateTime من عمود DATE (اتجاه D-112: التحويل عند الحدود) وتُعرض بصيغة تاريخ.
/// غير المخصص يظهر للقبض فقط — الصرف لا يُخصَّص أبداً.
/// </summary>
public sealed record PaymentListItem(
    int Id,
    int ReceiptNo,
    PaymentKind Kind,
    string StudentName,
    string? PayerName,
    long AmountCentimes,
    DateTime PaidOn,
    string? Note,
    long AllocatedCentimes)
{
    public string KindText => Kind == PaymentKind.Receipt ? "قبض" : "صرف";
    public string ReceiptNoText => $"#{ReceiptNo:000000}";
    public long UnallocatedCentimes => Kind == PaymentKind.Receipt ? AmountCentimes - AllocatedCentimes : 0;
    public bool HasUnallocated => UnallocatedCentimes > 0;
}

/// <summary>إيصال قبض بحرّية > 0 (6.6 — ز-1): المبلغ − Σ تخصيصاته — الأقدم أولاً لاستهلاك الزائدة · الصرف غير مربوط بإيصال فسقف إجمالي الزائدة حارسه في المصفف</summary>
public sealed record UnallocatedReceiptRaw(int PaymentId, long FreeCentimes);