using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Payroll;

/// <summary>DTOs كشوف الأجور (F5 — الشريحة 5.2) — النصوص العربية للعرض تُحسب هنا كما في PayrollDtos.</summary>
public sealed record PayrollRunListItem(
    int Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    RunStatus Status,
    long TotalCentimes,
    int LinesCount,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc)
{
    public string PeriodText => $"{PeriodStart:yyyy-MM-dd} ← {PeriodEnd:yyyy-MM-dd}";
    public string StatusText => Status == RunStatus.Approved ? "معتمد" : "مسودة";
    public bool IsDraft => Status == RunStatus.Draft;
}

public sealed record PayrollLineItem(
    int Id,
    int RunId,
    PayeeKind PayeeKind,
    int? TeacherId,      // 5.3-هـ: الصرف يحتاج معرف المستفيد لا اسمه فقط
    int? EmployeeId,     // 5.3-هـ
    string PayeeName,
    int? PolicyId,
    PayPolicyKind? Kind,
    long? RateCentimes,
    decimal? Percentage,
    bool? CountsUnjustifiedAbsent,
    decimal Quantity,
    LineSourceKind SourceKind,
    string Details,
    long AmountCentimes)
{
    public string PayeeKindText => PayeeKind == PayeeKind.Teacher ? "أستاذ" : "موظف";
    public bool IsManual => SourceKind == LineSourceKind.Manual;
    public bool IsNegative => AmountCentimes < 0;   // خصم — يُعرض بالأحمر في الشاشة (هـ)

    public string KindText => SourceKind == LineSourceKind.Manual
        ? (AmountCentimes >= 0 ? "يدوي — مكافأة" : "يدوي — خصم")
        : Kind switch
        {
            PayPolicyKind.PerPresentStudent => "لكل حاضر",
            PayPolicyKind.Percentage => "نسبة مئوية",
            PayPolicyKind.PerHour => "بالساعة",
            PayPolicyKind.PerDay => "باليوم",
            PayPolicyKind.PerMonth => "شهري ثابت",
            _ => "—",
        };
}

public sealed record PayrollRunDetails(
    PayrollRunListItem Run,
    IReadOnlyList<PayrollLineItem> Lines);