using EduMaster.Domain.Common;

namespace EduMaster.Domain.Payroll;

/// <summary>
/// إيصال صرف أجر (D-116/D-125) — وثيقة مالية: لا تعديل ولا حذف أبداً، الخطأ يُقابل بقيد عكسي سالب بملاحظة توثيقية (روح D-109).
/// يخصم من الرصيد الجاري للمستفيد (Σ معتمد − Σ مصروف عبر التاريخ — الترحيل تلقائي) · PayrollRunId مرجع معلوماتي اختياري فقط ·
/// ReceiptNo من تسلسل موحّد بلا فجوات (MAX+1 في معاملة الـHandler — مرآة D-105) · المبلغ موجب دائماً إلا قيد التصحيح.
/// </summary>
public sealed class Payout
{
    private bool _idSet;

    public int Id { get; private set; }
    public int ReceiptNo { get; private set; }

    // المستفيد — بالضبط أحدهما (مرآة قيد CK_Payouts_OnePayee)
    public PayeeKind PayeeKind { get; private set; }
    public int? TeacherId { get; private set; }
    public int? EmployeeId { get; private set; }

    public int? PayrollRunId { get; private set; }   // «ضمن كشف…» — معلوماتي: الصرف على الرصيد الجاري لا على السطر
    public long AmountCentimes { get; private set; }  // موجب = صرف · سالب = قيد تصحيح (بملاحظة إلزامية)
    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }

    public bool IsCorrection => AmountCentimes < 0;

    private Payout() { }

    public static Payout Create(
        PayeeKind payeeKind, int? teacherId, int? employeeId, int? payrollRunId,
        long amountCentimes, string? note, int receiptNo,
        DateTime createdAtUtc, int? createdByUserId)
    {
        GuardPayee(payeeKind, teacherId, employeeId);
        if (amountCentimes == 0)
            throw new DomainException("مبلغ الإيصال لا يمكن أن يكون صفراً.");
        if (amountCentimes < 0 && string.IsNullOrWhiteSpace(note))
            throw new DomainException("قيد التصحيح (بالمبلغ السالب) يتطلب ملاحظة توثيقية — اذكر رقم الإيصال المصحَّح.");
        if (payrollRunId is <= 0)
            throw new DomainException("مرجع الكشف غير صالح.");
        if (receiptNo <= 0)
            throw new DomainException("رقم الإيصال يجب أن يكون أكبر من صفر.");
        if (note is not null && note.Trim().Length > 200)
            throw new DomainException("الملاحظة طويلة جداً (الحد الأقصى 200 حرف).");

        return new Payout
        {
            PayeeKind = payeeKind,
            TeacherId = teacherId,
            EmployeeId = employeeId,
            PayrollRunId = payrollRunId,
            AmountCentimes = amountCentimes,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ReceiptNo = receiptNo,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    private static void GuardPayee(PayeeKind payeeKind, int? teacherId, int? employeeId)
    {
        if (!Enum.IsDefined(payeeKind))
            throw new DomainException("نوع المستفيد غير صالح.");
        if (payeeKind == PayeeKind.Teacher && (teacherId is null or <= 0 || employeeId is not null))
            throw new DomainException("إيصال الأستاذ يجب أن يرتبط بأستاذ فقط.");
        if (payeeKind == PayeeKind.Employee && (employeeId is null or <= 0 || teacherId is not null))
            throw new DomainException("إيصال الموظف يجب أن يرتبط بموظف فقط.");
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
    private Payout(int id, int receiptNo, PayeeKind payeeKind, int? teacherId, int? employeeId, int? payrollRunId,
        long amountCentimes, string? note, DateTime createdAtUtc, int? createdByUserId)
    {
        Id = id; _idSet = true;
        ReceiptNo = receiptNo;
        PayeeKind = payeeKind;
        TeacherId = teacherId;
        EmployeeId = employeeId;
        PayrollRunId = payrollRunId;
        AmountCentimes = amountCentimes;
        Note = note;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public static Payout Load(int id, int receiptNo, PayeeKind payeeKind, int? teacherId, int? employeeId, int? payrollRunId,
        long amountCentimes, string? note, DateTime createdAtUtc, int? createdByUserId)
        => new(id, receiptNo, payeeKind, teacherId, employeeId, payrollRunId, amountCentimes, note, createdAtUtc, createdByUserId);
}