using EduMaster.Domain.Enums;
using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Payroll;

/// <summary>
/// وقائع خام لمحرك الأجور (5.2) — تجمعها خدمة الاحتساب من منفذ القراءات مُصفّاةً من الفترات المعتمدة (س-3).
/// بلا أي تبعية على قاعدة — فيبقى المحرك نقياً قابلاً للاختبار (قاعدة 4.0).
/// </summary>
/// <param name="TeacherId">لقطة D-117 — <b>فارغة = أُقيمت قبل إسناد أستاذ لفوجها</b>: تُستبعد من الأجر وتُذكر في التحذيرات (لا اختفاء صامت).</param>
public sealed record PayrollSessionFact(int SessionId, int ClassGroupId, string ClassGroupName, int? TeacherId, DateTime StartsAt, int DurationMinutes);

/// <summary>علامة حضور لحصة مُقامة + السعر المتفق لصاحبها من تسجيله (لقطة D-52 — أساس النسبة المئوية).</summary>
public sealed record PayrollAttendanceFact(int SessionId, AttendanceStatus Status, long AgreedUnitPriceCentimes);

/// <summary>يوم عمل خام لموظف — يُجمَّع عدداً في الخدمة (حارس «لا مستقبل» وفرادة اليوم سبقاه في 5.1).</summary>
public sealed record PayrollWorkDayFact(int EmployeeId, DateOnly WorkDate);

/// <summary>مواصفة سطر محسوب جاهزة للكيان — لقطة سياسة كاملة بلا اسم (يُحلّ في الخدمة).</summary>
public sealed record ComputedLineSpec(
    PayeeKind PayeeKind, int? TeacherId, int? EmployeeId,
    int PolicyId, PayPolicyKind Kind, long RateCentimes, decimal? Percentage, bool CountsUnjustifiedAbsent,
    decimal Quantity, string Details, long AmountCentimes);

/// <summary>حصص بلا سياسة مغطية (أستاذ بلا افتراضية والفوج بلا تجاوز) — تُعاد تحذيراً ولا تُسقَط بصمت.</summary>
public sealed record UnpaidGroupWarning(int TeacherId, string ClassGroupName, int SessionsCount);

/// <summary>حصيلة الحساب النقي: سطور جاهزة + تحذيرات خام.</summary>
public sealed record PayrollComputationResult(IReadOnlyList<ComputedLineSpec> Lines, IReadOnlyList<UnpaidGroupWarning> Warnings);