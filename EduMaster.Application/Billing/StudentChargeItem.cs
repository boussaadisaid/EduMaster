using EduMaster.Domain.Enums;

namespace EduMaster.Application.Billing;

/// <summary>مستحق في قسم «المالية» بلوحة الطالب — قراءة مسطّحة (D-40) · 4.2: المخصوص والمتبقي (D-109)</summary>
public sealed record StudentChargeItem(
    int Id,
    int StudentId,
    ChargeKind Kind,
    string SourceDescription,
    long OriginalAmountCentimes,
    long AmountCentimes,
    ChargeStatus Status,
    string? AdjustmentNote,
    DateTime CreatedAtUtc,
    long AllocatedCentimes,
    int? AcademicYearId = null,
    string? AcademicYearName = null)
{
    public string KindText => Kind == ChargeKind.RegistrationFee ? "حقوق تسجيل" : "حزمة حصص";
    public string StatusText => Status == ChargeStatus.Active ? "فعّال" : "ملغى";
    public bool IsActive => Status == ChargeStatus.Active;
    /// <summary>المتبقي للعرض (6.6-ع-2): الملغى لا يعرض متبقّياً حياً — يُعرض «—» (موثق وغير محسوب — D-108/D-109)</summary>
    public long? RemainingForDisplayCentimes => IsActive ? RemainingCentimes : null;

    /// <summary>المتبقي = الحالي − المخصوص (D-109) — على الفعّال فقط معنى له</summary>
    public long RemainingCentimes => AmountCentimes - AllocatedCentimes;
}