using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Payroll;

/// <summary>صف قراءة مسطّح ليوم عمل — WorkDate تُقرأ DateTime من عمود DATE (اتفاق القراءة مع D-112)</summary>
public sealed record WorkLogItem(
    int Id,
    int EmployeeId,
    DateTime WorkDate,
    string? Note);

/// <summary>صف قراءة مسطّح لسياسة أجر (D-40) — باسم المستفيد واسم الفوج من الربط</summary>
public sealed record PayPolicyItem(
    int Id,
    PayeeKind PayeeKind,
    int? TeacherId,
    int? EmployeeId,
    string PayeeName,
    int? ClassGroupId,
    string? ClassGroupName,
    PayPolicyKind Kind,
    long RateCentimes,
    decimal? Percentage,
    bool CountsUnjustifiedAbsent,
    bool IsActive)
{
    public string KindText => Kind switch
    {
        PayPolicyKind.PerPresentStudent => "لكل حاضر",
        PayPolicyKind.Percentage => "نسبة مئوية",
        PayPolicyKind.PerHour => "بالساعة",
        PayPolicyKind.PerDay => "باليوم",
        PayPolicyKind.PerMonth => "شهري ثابت",
        _ => Kind.ToString()
    };

    public string ScopeText => ClassGroupId is null ? "افتراضية" : $"تجاوز: {ClassGroupName}";
}
