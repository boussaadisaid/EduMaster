using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>عقد مستودع كشوف الأجور — الكشف وسطوره تُحفظ ذرّياً في معاملة الـHandler (D-33).</summary>
public interface IPayrollRunRepository
{
    Task<PayrollRun?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollRun>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>هل توجد فترة معتمدة تتقاطع مع المجال؟ حارس «لا ازدواج احتساب» — فحص تطبيقي بروح D-27 (لا فهرس يعبّر عن تداخل مجالات).</summary>
    Task<bool> ExistsApprovedOverlapAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default);

    /// <summary>هل توجد مسودة تتقاطع مع المجال؟ حارس «لا تكديس مسودات» — مسودة الفترة تُعاد حسابها (🔁) أو تُحذف (🗑)، لا تُكرَّر.</summary>
    Task<bool> ExistsDraftOverlapAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default);

    Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default);

    /// <summary>يحدّث الإجمالي وختم الاعتماد — السطور تُدار عبر مستودعها.</summary>
    Task UpdateAsync(PayrollRun run, CancellationToken cancellationToken = default);

    /// <summary>حذف مسودة فقط (الحارس في الـHandler) — سطورها تتبعها بتتابع الحذف (CASCADE في 016).</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>عقد مستودع سطور الكشوف — إضافة جماعية + حذف المحسوبة لإعادة الحساب (روح D-101) + حذف سطر يدوي + تجميع الأعداد + مجاميع المعتمد للأرصدة (5.3).</summary>
public interface IPayrollLineRepository
{
    Task<IReadOnlyList<PayrollLine>> GetByRunAsync(int runId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyList<PayrollLine> lines, CancellationToken cancellationToken = default);

    /// <summary>يحذف السطور المحسوبة فقط لمسودة — اليدوية تنجو (س-8).</summary>
    Task DeleteComputedForRunAsync(int runId, CancellationToken cancellationToken = default);

    /// <summary>حذف سطر يدوي واحد من مسودة.</summary>
    Task DeleteAsync(int lineId, CancellationToken cancellationToken = default);

    /// <summary>عدد سطور كل كشف (تجميعي) — لقائمة شاشة «💼 الأجور».</summary>
    Task<IReadOnlyDictionary<int, int>> GetCountsByRunAsync(CancellationToken cancellationToken = default);

    /// <summary>مجاميع السطور المعتمدة لكل مستفيد (Σ على كشوف Status=2 فقط — 5.3: طرف «المعتمد» من الرصيد الجاري).</summary>
    Task<IReadOnlyList<PayeeApprovedTotal>> GetApprovedTotalsByPayeeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// منفذ قراءات الاحتساب (5.2-ب) — قراءات مسطّحة خام عبر الجداول للمحرك (روح D-40):
/// بلا كيانات (عدا السياسات فتُدار حُراسها مركزياً) · التصفية من الفترات المعتمدة تتم في خدمة الاحتساب لا هنا.
/// يعيش على عقد جديد خاص (لا توسيع لعقود الجلسات/الحضور/السياسات القائمة) حتى يبقى البناء أخضر قبل تنفيذ 5.2-ج.
/// الحصص المُقامة تصل <b>بما فيها اللقطة الفارغة</b> (أُقيمت بلا أستاذ مسند) — تُحصى في التحذيرات ولا تختفي بصمت (D-124).
/// </summary>
public interface IPayrollFactsRepository
{
    /// <summary>السياسات الفعّالة كلها (أساتذة وموظفون، افتراضية وتجاوزات) — كيانات.</summary>
    Task<IReadOnlyList<PayPolicy>> GetAllActivePoliciesAsync(CancellationToken cancellationToken = default);

    /// <summary>الحصص المُقامة (Status=2) في فترة [من/إلى شاملان] + اسم الفوج + لقطة الأستاذ (D-117 — قابلة للعدم).</summary>
    Task<IReadOnlyList<PayrollSessionFact>> GetHeldSessionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>علامات حضور حصص الفترة المُقامة + السعر المتفق لصاحبها من تسجيله (لقطة D-52 — أساس النسبة) — عبر SessionAttendance.ClassGroupEnrollmentId.</summary>
    Task<IReadOnlyList<PayrollAttendanceFact>> GetAttendanceFactsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>أيام العمل في فترة [شاملان] — (الموظف، التاريخ) خام.</summary>
    Task<IReadOnlyList<PayrollWorkDayFact>> GetWorkDayFactsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}