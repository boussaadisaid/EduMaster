using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Payroll;

/// <summary>
/// خدمة الاحتساب (5.2): تجمع الوقائع من منفذ القراءات، تصفّي مصادر الفترات المعتمدة (س-3 — المحسوب لا يُحسب مرتين)،
/// تستدعي المحرك النقي، ثم تحلّ أسماء المستفيدين (لقطة السطر — D-52 ممتدة) وتصوغ التحذيرات بثلاث عائلات:
/// حصص بلا تغطية في سياسة صاحبها (من المحرك) + حصص لأستاذ بلا أي سياسة فعّالة + حصص بلقطة أستاذ فارغة (أُقيمت قبل الإسناد) —
/// لا اختفاء صامت لعملٍ أُنجز. يستخدمها handler التوليد وإعادة الحساب معاً — نفس الأرقام في المسارين.
/// </summary>
public sealed record ComputedLineWithName(string PayeeName, ComputedLineSpec Spec);

public sealed record PayrollComputationOutcome(IReadOnlyList<ComputedLineWithName> Lines, IReadOnlyList<string> Warnings);

public sealed class PayrollComputationService
{
    private readonly IPayrollFactsRepository _facts;
    private readonly IPayrollRunRepository _runs;
    private readonly ITeacherRepository _teachers;
    private readonly IEmployeeRepository _employees;

    public PayrollComputationService(
        IPayrollFactsRepository facts,
        IPayrollRunRepository runs,
        ITeacherRepository teachers,
        IEmployeeRepository employees)
    {
        _facts = facts;
        _runs = runs;
        _teachers = teachers;
        _employees = employees;
    }

    public async Task<PayrollComputationOutcome> ComputeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        // 1) الفترات المعتمدة — مصادرها مستبعدة من أي مسودة (س-3)
        var approvedPeriods = (await _runs.GetAllAsync(cancellationToken))
            .Where(r => r.IsApproved)
            .Select(r => (r.PeriodStart, r.PeriodEnd))
            .ToList();

        // 2) السياسات الفعّالة كلها (أساتذة وموظفون)
        var policies = await _facts.GetAllActivePoliciesAsync(cancellationToken);

        // 3) الحصص المُقامة بلقطة أستاذها (D-117) — مُصفّاة من المعتمد · اللقطة الفارغة تصل وتُحصى (لا تُرشَّح)
        var sessions = (await _facts.GetHeldSessionsAsync(from, to, cancellationToken))
            .Where(s => !InApproved(DateOnly.FromDateTime(s.StartsAt), approvedPeriods))
            .ToList();
        var sessionIds = sessions.Select(s => s.SessionId).ToHashSet();

        // 4) علامات حضور تلك الحصص فقط
        var attendance = (await _facts.GetAttendanceFactsAsync(from, to, cancellationToken))
            .Where(a => sessionIds.Contains(a.SessionId))
            .ToList();

        // 5) أيام العمل — مُصفّاة من المعتمد، مجمّعة عدداً لكل موظف
        var workDayCounts = (await _facts.GetWorkDayFactsAsync(from, to, cancellationToken))
            .Where(d => !InApproved(d.WorkDate, approvedPeriods))
            .GroupBy(d => d.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Count());

        // 6) الحساب النقي (قاعدة 4.0 — بلا قاعدة) · اللقطة الفارغة لا تطابق أي أستاذ فلا تُنتج سطراً
        var result = PayrollCalculator.Compute(policies, sessions, attendance, workDayCounts);

        // 7) حل الأسماء (لقطة السطر) + صياغة التحذيرات
        var teacherNames = (await _teachers.SearchAsync(null, cancellationToken)).ToDictionary(t => t.Id, t => t.FullName);
        var employeeNames = (await _employees.SearchAsync(null, cancellationToken)).ToDictionary(e => e.Id, e => e.FullName);

        var lines = result.Lines
            .Select(l => new ComputedLineWithName(ResolveName(l, teacherNames, employeeNames), l))
            .ToList();

        var warnings = result.Warnings
            .Select(w => $"«{teacherNames.GetValueOrDefault(w.TeacherId, $"أستاذ #{w.TeacherId}")}»: {w.SessionsCount} حصص في فوج «{w.ClassGroupName}» بلا سياسة مغطية — لم تدخل الكشف · الفعل: أضِف سياسة افتراضية أو تجاوزاً من زر «💼 الأجر» في شاشة الأساتذة ثم أعد الحساب 🔁")
            .ToList();

        // حصص بلقطة أستاذ فارغة — أُقيمت قبل إسناد أستاذ لفوجها (D-117) — كشف المستخدم 2026-08-25: كانت تختفي بصمت
        foreach (var group in sessions.Where(s => s.TeacherId is null).GroupBy(s => s.ClassGroupName))
            warnings.Add($"{group.Count()} حصص في فوج «{group.Key}» أُقيمت بلا أستاذ مسند لحظتها (لقطة فارغة — D-117) — لا تدخل الأجور · إن أقامها أستاذ فعلاً فالمساران: صحّح اللقطة من شاشة الحصص (زر «🔧 لقطة الأستاذ» عليها) ثم أعد الحساب 🔁 · أو عوّضه بسطر يدوي ➕ في هذه المسودة قبل الاعتماد");

        // أساتذة أقاموا حصصاً بلا أي سياسة فعّالة أصلاً
        var coveredTeacherIds = policies
            .Where(p => p.PayeeKind == PayeeKind.Teacher)
            .Select(p => p.TeacherId!.Value)
            .ToHashSet();

        foreach (var group in sessions.Where(s => s.TeacherId is not null && !coveredTeacherIds.Contains(s.TeacherId!.Value)).GroupBy(s => s.TeacherId!.Value))
        {
            var name = teacherNames.GetValueOrDefault(group.Key, $"أستاذ #{group.Key}");
            warnings.Add($"«{name}»: {group.Count()} حصص مُقامة بلا أي سياسة أجر فعّالة — لم تدخل الكشف · الفعل: أنشئ السياسة من زر «💼 الأجر» في شاشة الأساتذة ثم أعد الحساب 🔁");
        }

        return new PayrollComputationOutcome(lines, warnings);
    }

    private static bool InApproved(DateOnly date, List<(DateOnly Start, DateOnly End)> periods)
        => periods.Any(p => date >= p.Start && date <= p.End);

    private static string ResolveName(ComputedLineSpec line, Dictionary<int, string> teacherNames, Dictionary<int, string> employeeNames)
        => line.PayeeKind == PayeeKind.Teacher
            ? teacherNames.GetValueOrDefault(line.TeacherId!.Value, $"أستاذ #{line.TeacherId}")
            : employeeNames.GetValueOrDefault(line.EmployeeId!.Value, $"موظف #{line.EmployeeId}");
}