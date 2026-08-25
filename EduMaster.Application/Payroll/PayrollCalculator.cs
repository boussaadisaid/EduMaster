using EduMaster.Domain.Enums;
using EduMaster.Domain.Payroll;
using System.Globalization;

namespace EduMaster.Application.Payroll;

/// <summary>
/// محرك الاحتساب (D-123/س-2) — حساب نقي بلا قاعدة بيانات (يُختبر بأمثلة عددية في 5.2-د).
/// الصيغ الخمس حرفياً من D-113/D-114:
///   لكل حاضر: المحسوبون (حاضر + غائب غير مبرر إن رُفع العلم) × القيمة — التجاوز على الفوج أولاً ثم الافتراضية ·
///   نسبة مئوية: Σ(السعر المتفق لكل محسوب — لقطة D-52) × النسبة · بالساعة: Σ دقائق الحصص × القيمة ÷ 60 ·
///   باليوم: أيام العمل × القيمة · شهري ثابت: القيمة كاملة دائماً.
/// التقريب لأقرب سنتيم (AwayFromZero) · سطر مبلغه صفر لا يُولَّد · حصص بلا سياسة مغطية تُعاد تحذيراً ولا تُسقَط بصمت.
/// </summary>
public static class PayrollCalculator
{
    public static PayrollComputationResult Compute(
        IReadOnlyList<PayPolicy> activePolicies,
        IReadOnlyList<PayrollSessionFact> sessions,
        IReadOnlyList<PayrollAttendanceFact> attendance,
        IReadOnlyDictionary<int, int> employeeWorkDayCounts)
    {
        ArgumentNullException.ThrowIfNull(activePolicies);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(attendance);
        ArgumentNullException.ThrowIfNull(employeeWorkDayCounts);

        var lines = new List<ComputedLineSpec>();
        var warnings = new List<UnpaidGroupWarning>();
        var attendanceBySession = attendance.GroupBy(a => a.SessionId).ToDictionary(g => g.Key, g => g.ToList());

        // ---------- الأساتذة: لقطة D-117 نسبت كل حصة لمن أقامها فعلاً ----------
        foreach (var teacherPolicies in activePolicies.Where(p => p.PayeeKind == PayeeKind.Teacher).GroupBy(p => p.TeacherId!.Value))
        {
            var teacherId = teacherPolicies.Key;
            var teacherSessions = sessions.Where(s => s.TeacherId == teacherId).ToList();
            if (teacherSessions.Count == 0)
                continue;

            var defaultPolicy = teacherPolicies.FirstOrDefault(p => p.ClassGroupId is null);
            var overrides = teacherPolicies.Where(p => p.ClassGroupId is not null).ToList();

            // تجميع الحصص حسب السياسة المطبَّقة — التجاوز على الفوج أولاً ثم الافتراضية (D-113)
            var perPolicy = new Dictionary<PayPolicy, List<PayrollSessionFact>>();
            var unpaidByGroup = new Dictionary<string, int>();

            foreach (var session in teacherSessions)
            {
                var policy = overrides.FirstOrDefault(o => o.ClassGroupId == session.ClassGroupId) ?? defaultPolicy;
                if (policy is null)
                {
                    unpaidByGroup[session.ClassGroupName] = unpaidByGroup.GetValueOrDefault(session.ClassGroupName) + 1;
                    continue;
                }
                if (!perPolicy.TryGetValue(policy, out var list))
                    perPolicy[policy] = list = new List<PayrollSessionFact>();
                list.Add(session);
            }

            foreach (var (groupName, count) in unpaidByGroup)
                warnings.Add(new UnpaidGroupWarning(teacherId, groupName, count));

            foreach (var (policy, policySessions) in perPolicy)
            {
                var line = ComputeTeacherLine(teacherId, policy, policySessions, attendanceBySession);
                if (line is not null)
                    lines.Add(line);
            }
        }

        // ---------- الموظفون: باليوم يستهلك سجل الأيام (D-115) · الشهري يُدرج كاملاً دائماً ----------
        foreach (var policy in activePolicies.Where(p => p.PayeeKind == PayeeKind.Employee))
        {
            var employeeId = policy.EmployeeId!.Value;
            switch (policy.Kind)
            {
                case PayPolicyKind.PerDay:
                    {
                        var days = employeeWorkDayCounts.GetValueOrDefault(employeeId);
                        if (days == 0)
                            break;   // لا أيام في الفترة ⇒ لا مستحق
                        lines.Add(new ComputedLineSpec(PayeeKind.Employee, null, employeeId,
                            policy.Id, policy.Kind, policy.RateCentimes, null, false,
                            days, $"{days} أيام عمل × {Dinars(policy.RateCentimes)} دج",
                            policy.RateCentimes * days));
                        break;
                    }
                case PayPolicyKind.PerMonth:
                    lines.Add(new ComputedLineSpec(PayeeKind.Employee, null, employeeId,
                        policy.Id, policy.Kind, policy.RateCentimes, null, false,
                        1, "شهري ثابت", policy.RateCentimes));
                    break;
                    // PerPresentStudent/Percentage/PerHour للأساتذة فقط — الواجهة تمنعها للموظفين (دفاع إضافي هنا)
            }
        }

        return new PayrollComputationResult(lines, warnings);
    }

    private static ComputedLineSpec? ComputeTeacherLine(
        int teacherId, PayPolicy policy, List<PayrollSessionFact> policySessions,
        IReadOnlyDictionary<int, List<PayrollAttendanceFact>> attendanceBySession)
    {
        // نطاق التجاوز فوج واحد بالضرورة — يُسمّى في التفصيل؛ الافتراضية تجمع أفواجاً فلا تُسمّى
        var scopePrefix = policy.ClassGroupId is not null ? $"فوج «{policySessions[0].ClassGroupName}» — " : string.Empty;

        switch (policy.Kind)
        {
            case PayPolicyKind.PerPresentStudent:
                {
                    var counted = policySessions.Sum(s => CountedIn(s).Count);
                    if (counted == 0) return null;
                    return new ComputedLineSpec(PayeeKind.Teacher, teacherId, null,
                        policy.Id, policy.Kind, policy.RateCentimes, null, policy.CountsUnjustifiedAbsent,
                        counted, $"{scopePrefix}{policySessions.Count} حصص × {counted} محسوباً × {Dinars(policy.RateCentimes)} دج",
                        policy.RateCentimes * counted);
                }
            case PayPolicyKind.Percentage:
                {
                    var countedFacts = policySessions.SelectMany(s => CountedIn(s)).ToList();
                    var basis = countedFacts.Sum(m => m.AgreedUnitPriceCentimes);   // السعر المتفق لكل محسوب (لقطة D-52)
                    if (basis == 0) return null;
                    var percentage = policy.Percentage!.Value;
                    return new ComputedLineSpec(PayeeKind.Teacher, teacherId, null,
                        policy.Id, policy.Kind, policy.RateCentimes, percentage, policy.CountsUnjustifiedAbsent,
                        countedFacts.Count, $"{scopePrefix}{policySessions.Count} حصص × أساس {Dinars(basis)} دج × {percentage}%",
                        RoundCentimes(basis * percentage / 100m));
                }
            case PayPolicyKind.PerHour:
                {
                    var minutes = policySessions.Sum(s => s.DurationMinutes);
                    if (minutes == 0) return null;
                    return new ComputedLineSpec(PayeeKind.Teacher, teacherId, null,
                        policy.Id, policy.Kind, policy.RateCentimes, null, policy.CountsUnjustifiedAbsent,
                        minutes / 60m, $"{scopePrefix}{policySessions.Count} حصص × {minutes / 60m:0.##} ساعة × {Dinars(policy.RateCentimes)} دج/ساعة",
                        RoundCentimes(policy.RateCentimes * (minutes / 60m)));
                }
            default:
                return null;   // باليوم/الشهري للموظفين فقط
        }

        // المحسوبون في حصة: حاضر + غائب غير مبرر إن رُفع علم السياسة (D-114) — المبرر لا يُحسب أبداً
        List<PayrollAttendanceFact> CountedIn(PayrollSessionFact session)
            => attendanceBySession.TryGetValue(session.SessionId, out var marks)
                ? marks.Where(m => m.Status == AttendanceStatus.Present
                    || (policy.CountsUnjustifiedAbsent && m.Status == AttendanceStatus.Absent)).ToList()
                : new List<PayrollAttendanceFact>();
    }

    private static long RoundCentimes(decimal centimes) => (long)Math.Round(centimes, 0, MidpointRounding.AwayFromZero);

    private static string Dinars(long centimes) => (centimes / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}