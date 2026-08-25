using EduMaster.Domain.Common;

namespace EduMaster.Domain.Payroll;

/// <summary>
/// كشف أجور فترة (D-116/D-123): مسودة قابلة لإعادة الحساب الذرّية ← اعتماد يقفل نهائياً (لا تعديل ولا حذف بعده — الخطأ يُصحَّح بصرف تسوية) ·
/// الإجمالي يُصان مع كل إضافة/حذف سطر · يملك سطوره (تُحفظ معه ذرّياً في معاملة الـHandler — D-33) · السطور اليدوية تنجو من إعادة الحساب.
/// </summary>
public sealed class PayrollRun
{
    private readonly List<PayrollLine> _lines = new();
    private bool _idSet;

    public int Id { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public RunStatus Status { get; private set; }
    public long TotalCentimes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public int? ApprovedByUserId { get; private set; }

    public IReadOnlyList<PayrollLine> Lines => _lines;

    public bool IsDraft => Status == RunStatus.Draft;
    public bool IsApproved => Status == RunStatus.Approved;

    private PayrollRun() { }

    /// <summary>مسودة كشف لفترة — فحص «لا تداخل مع المعتمدة» في الـHandler (روح D-27).</summary>
    public static PayrollRun CreateDraft(DateOnly periodStart, DateOnly periodEnd, DateTime createdAtUtc, int? createdByUserId)
    {
        if (periodEnd < periodStart)
            throw new DomainException("نهاية الفترة لا يمكن أن تسبق بدايتها.");

        return new PayrollRun
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = RunStatus.Draft,
            TotalCentimes = 0,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    /// <summary>إضافة سطر (محسوب من المحرك أو يدوي بسبب) — مسموح في المسودة فقط، والإجمالي يُصان فوراً.</summary>
    public void AddLine(PayrollLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!IsDraft)
            throw new DomainException("لا يمكن تعديل كشف معتمد.");
        if (line.RunId != Id)
            throw new DomainException("السطر لا يتبع هذا الكشف.");

        _lines.Add(line);
        TotalCentimes += line.AmountCentimes;
    }

    /// <summary>إزالة سطر يدوي من المسودة (مكافأة أُدخلت بالخطأ مثلاً) — المحسوبة تُزال بإعادة الحساب فقط.</summary>
    public void RemoveManualLine(PayrollLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!IsDraft)
            throw new DomainException("لا يمكن تعديل كشف معتمد.");
        if (line.SourceKind != LineSourceKind.Manual)
            throw new DomainException("السطور المحسوبة تُزال بإعادة الحساب فقط.");
        if (!_lines.Remove(line))
            throw new DomainException("السطر غير موجود في هذا الكشف.");

        TotalCentimes -= line.AmountCentimes;
    }

    /// <summary>إعادة الحساب الذرّية (روح D-101): تسقط المحسوبة وتُبقي اليدوية — يعيد المحرك توليد المحسوبة بعدها.</summary>
    public void ClearComputedLines()
    {
        if (!IsDraft)
            throw new DomainException("لا يمكن تعديل كشف معتمد.");

        foreach (var line in _lines.Where(l => l.SourceKind == LineSourceKind.Computed).ToList())
        {
            _lines.Remove(line);
            TotalCentimes -= line.AmountCentimes;
        }
    }

    /// <summary>الاعتماد — نقطة اللاعودة: يختم من اعتمد ومتى، ويُقفل كل شيء.</summary>
    public void Approve(DateTime approvedAtUtc, int? approvedByUserId)
    {
        if (IsApproved)
            throw new DomainException("الكشف معتمد بالفعل.");
        if (_lines.Count == 0)
            throw new DomainException("لا يمكن اعتماد كشف بلا سطور — احذفه بدل اعتماده.");

        Status = RunStatus.Approved;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedByUserId = approvedByUserId;
    }

    /// <summary>يُستدعى من المستودع فقط بعد الإدراج.</summary>
    internal void SetId(int id)
    {
        if (_idSet || id <= 0)
            throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }

    /// <summary>يستبدل سطور الكشف بالمحمَّلة من القاعدة (لعرض التفاصيل) — تحميل فقط، لا يمرّ على الحُراس.</summary>
    public void LoadLines(IReadOnlyList<PayrollLine> lines)
    {
        _lines.Clear();
        if (lines is not null)
            _lines.AddRange(lines);
    }

    // إعادة تحميل من القاعدة — بلا حُراس (مرآة المخزَّن)
    private PayrollRun(
        int id, DateOnly periodStart, DateOnly periodEnd, RunStatus status, long totalCentimes,
        DateTime createdAtUtc, int? createdByUserId, DateTime? approvedAtUtc, int? approvedByUserId)
    {
        Id = id; _idSet = true;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Status = status;
        TotalCentimes = totalCentimes;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedByUserId = approvedByUserId;
    }

    public static PayrollRun Load(
        int id, DateOnly periodStart, DateOnly periodEnd, RunStatus status, long totalCentimes,
        DateTime createdAtUtc, int? createdByUserId, DateTime? approvedAtUtc, int? approvedByUserId)
        => new(id, periodStart, periodEnd, status, totalCentimes, createdAtUtc, createdByUserId, approvedAtUtc, approvedByUserId);
}