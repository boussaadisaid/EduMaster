using EduMaster.Domain.Common;

namespace EduMaster.Domain.Payroll;

/// <summary>
/// سطر في كشف أجور — سنابشوت كامل للسياسة لحظة الاحتساب (روح D-52) فيبقى الكشف المعتمد صحيحاً حتى لو تغيّرت السياسة لاحقاً ·
/// محسوب (من المحرك — تفصيله نص مولَّد) أو يدوي (مكافأة+/خصم− بسبب إلزامي — D-123/س-8) ·
/// السطور لا تُعدَّل أبداً: إعادة الحساب تحذف المحسوبة وتعيد توليدها، واليدوية تُحذف وتُضاف من جديد.
/// </summary>
public sealed class PayrollLine
{
    private bool _idSet;

    public int Id { get; private set; }
    public int RunId { get; private set; }

    // المستفيد — بالضبط أحدهما (مرآة قيد CK_PayrollLines_OnePayee)
    public PayeeKind PayeeKind { get; private set; }
    public int? TeacherId { get; private set; }
    public int? EmployeeId { get; private set; }

    public string PayeeName { get; private set; } = string.Empty;   // لقطة الاسم — يبقى الكشف مقروءاً ولو تغيّر الاسم لاحقاً

    // لقطة السياسة (للمحسوبة) — كلها NULL لليدوية
    public int? PolicyId { get; private set; }
    public PayPolicyKind? Kind { get; private set; }
    public long? RateCentimes { get; private set; }
    public decimal? Percentage { get; private set; }
    public bool? CountsUnjustifiedAbsent { get; private set; }

    public decimal Quantity { get; private set; }          // عدد المحسوبين / الساعات / الأيام — 0 للشهري واليدوي
    public LineSourceKind SourceKind { get; private set; }
    public string Details { get; private set; } = string.Empty;   // تفصيل مولَّد للمحسوبة («3 حصص × 41 × 200») · سبب إلزامي لليدوية
    public long AmountCentimes { get; private set; }       // سالب مسموح لليدوية (خصم) فقط

    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }

    private PayrollLine() { }

    /// <summary>سطر محسوب من المحرك بلقطة السياسة الكاملة — المبلغ دائماً ≥ 0.</summary>
    public static PayrollLine CreateComputed(
        int runId, PayeeKind payeeKind, int? teacherId, int? employeeId, string payeeName,
        int policyId, PayPolicyKind kind, long rateCentimes, decimal? percentage, bool countsUnjustifiedAbsent,
        decimal quantity, string details, long amountCentimes,
        DateTime createdAtUtc, int? createdByUserId)
    {
        GuardPayee(payeeKind, teacherId, employeeId);
        if (string.IsNullOrWhiteSpace(payeeName))
            throw new DomainException("اسم المستفيد مطلوب في سطر الكشف.");
        if (policyId <= 0)
            throw new DomainException("السطر المحسوب يجب أن يشير إلى سياسته.");
        if (string.IsNullOrWhiteSpace(details))
            throw new DomainException("تفصيل السطر المحسوب مطلوب.");
        if (amountCentimes < 0)
            throw new DomainException("المبلغ المحسوب لا يمكن أن يكون سالباً.");

        return new PayrollLine
        {
            RunId = GuardRun(runId),
            PayeeKind = payeeKind,
            TeacherId = teacherId,
            EmployeeId = employeeId,
            PayeeName = payeeName.Trim(),
            PolicyId = policyId,
            Kind = kind,
            RateCentimes = rateCentimes,
            Percentage = percentage,
            CountsUnjustifiedAbsent = countsUnjustifiedAbsent,
            Quantity = quantity,
            SourceKind = LineSourceKind.Computed,
            Details = details.Trim(),
            AmountCentimes = amountCentimes,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    /// <summary>سطر يدوي (مكافأة + / خصم −) — سبب إلزامي ومبلغ غير صفري (س-8).</summary>
    public static PayrollLine CreateManual(
        int runId, PayeeKind payeeKind, int? teacherId, int? employeeId, string payeeName,
        long amountCentimes, string reason,
        DateTime createdAtUtc, int? createdByUserId)
    {
        GuardPayee(payeeKind, teacherId, employeeId);
        if (string.IsNullOrWhiteSpace(payeeName))
            throw new DomainException("اسم المستفيد مطلوب في سطر الكشف.");
        if (amountCentimes == 0)
            throw new DomainException("مبلغ السطر اليدوي لا يمكن أن يكون صفراً.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("اذكر سبب السطر اليدوي (مكافأة / خصم).");

        return new PayrollLine
        {
            RunId = GuardRun(runId),
            PayeeKind = payeeKind,
            TeacherId = teacherId,
            EmployeeId = employeeId,
            PayeeName = payeeName.Trim(),
            SourceKind = LineSourceKind.Manual,
            Details = reason.Trim(),
            AmountCentimes = amountCentimes,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    private static int GuardRun(int runId)
    {
        if (runId <= 0)
            throw new DomainException("سطر الكشف يجب أن يرتبط بكشف.");
        return runId;
    }

    private static void GuardPayee(PayeeKind payeeKind, int? teacherId, int? employeeId)
    {
        if (!Enum.IsDefined(payeeKind))
            throw new DomainException("نوع المستفيد غير صالح.");
        if (payeeKind == PayeeKind.Teacher && (teacherId is null or <= 0 || employeeId is not null))
            throw new DomainException("سطر الأستاذ يجب أن يرتبط بأستاذ فقط.");
        if (payeeKind == PayeeKind.Employee && (employeeId is null or <= 0 || teacherId is not null))
            throw new DomainException("سطر الموظف يجب أن يرتبط بموظف فقط.");
    }

    /// <summary>يُستدعى من المستودع فقط بعد الإدراج.</summary>
    internal void SetId(int id)
    {
        if (_idSet || id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }

    // إعادة تحميل من القاعدة — بلا حُراس (مرآة المخزَّن)
    private PayrollLine(
        int id, int runId, PayeeKind payeeKind, int? teacherId, int? employeeId, string payeeName,
        int? policyId, PayPolicyKind? kind, long? rateCentimes, decimal? percentage, bool? countsUnjustifiedAbsent,
        decimal quantity, LineSourceKind sourceKind, string details, long amountCentimes,
        DateTime createdAtUtc, int? createdByUserId)
    {
        Id = id; _idSet = true;
        RunId = runId;
        PayeeKind = payeeKind;
        TeacherId = teacherId;
        EmployeeId = employeeId;
        PayeeName = payeeName;
        PolicyId = policyId;
        Kind = kind;
        RateCentimes = rateCentimes;
        Percentage = percentage;
        CountsUnjustifiedAbsent = countsUnjustifiedAbsent;
        Quantity = quantity;
        SourceKind = sourceKind;
        Details = details;
        AmountCentimes = amountCentimes;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public static PayrollLine Load(
        int id, int runId, PayeeKind payeeKind, int? teacherId, int? employeeId, string payeeName,
        int? policyId, PayPolicyKind? kind, long? rateCentimes, decimal? percentage, bool? countsUnjustifiedAbsent,
        decimal quantity, LineSourceKind sourceKind, string details, long amountCentimes,
        DateTime createdAtUtc, int? createdByUserId)
        => new(id, runId, payeeKind, teacherId, employeeId, payeeName, policyId, kind, rateCentimes, percentage,
            countsUnjustifiedAbsent, quantity, sourceKind, details, amountCentimes, createdAtUtc, createdByUserId);
}