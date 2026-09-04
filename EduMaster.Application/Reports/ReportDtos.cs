using EduMaster.Application.Billing;   // StudentChargeItem + PaymentListItem
using EduMaster.Domain.Enums;          // PaymentKind + AttendanceStatus
using System.Globalization;            // تنسيق الساعات (ق-2) بلا ثقافة محلية

namespace EduMaster.Application.Reports;

/// <summary>فوج ظهر فعلياً داخل بيانات التقرير المحددة بالفترة — يُستخدم كمصدر وحيد لفلتر الفوج التاريخي.</summary>
public sealed record ReportGroupOption(int Id, string GroupName, string SubjectName, string LevelName);

// ═══ أشكال 6.1 — مُستعادة حرفياً من مستهلكيها السليمين (معالجا 6.1 + اختباراتهم الثلاثة + VMs الشاشة) — درس D-132 ═══

/// <summary>سطر تخصيص دفعة لكشف الحساب (D-128: الوصف يُركَّب في الـHandler من قائمة المستحقات — لا نص SQL مكرر)</summary>
public sealed record StudentPaymentAllocationLine(int ChargeId, string SourceDescription, long AmountCentimes);

/// <summary>سطر إيصال في كشف حساب الطالب — الإيصال الخام + تخصيصاته المركّبة · غير المخصص للقبض فقط (الصرف لا يُخصَّص أبداً)</summary>
public sealed record StudentPaymentLine(
    int Id,
    int ReceiptNo,
    PaymentKind Kind,
    string? PayerName,
    long AmountCentimes,
    DateTime PaidOn,
    string? Note,
    long AllocatedCentimes,
    IReadOnlyList<StudentPaymentAllocationLine> Allocations)
{
    /// <summary>المبلغ المخصص من هذا الإيصال لمستحقات السنة المحددة في كشف السنة؛ صفر في الكشف الكامل.</summary>
    public long AllocatedToSelectedAcademicYearCentimes { get; init; }
    public string KindText => Kind == PaymentKind.Receipt ? "قبض" : "صرف";
    public string ReceiptNoText => $"#{ReceiptNo:000000}";

    /// <summary>ما بقي من هذه الدفعة بلا تخصيص (زائدة متولدة D-107) — الصرف لا تخصيص له أبداً</summary>
    public long UnallocatedCentimes => Kind == PaymentKind.Receipt ? AmountCentimes - AllocatedCentimes : 0;
    public bool HasUnallocated => UnallocatedCentimes > 0;
}

/// <summary>كشف حساب طالب (6.1 — D-127): المستحقات + الإيصالات + الزائدة الدائنة (D-107) — الإجماليات خصائص محسوبة من الأسطر لا تُخزَّن (D-109)</summary>
public sealed record StudentStatementItem(
    IReadOnlyList<StudentChargeItem> Charges,
    IReadOnlyList<StudentPaymentLine> Payments,
    long CreditCentimes)
{
    public bool IsAcademicYearScoped { get; init; }
    public int? AcademicYearId { get; init; }
    public string? AcademicYearName { get; init; }
    /// <summary>إجمالي المقبوض — إيصالات القبض فقط</summary>
    public long ReceiptsTotalCentimes => IsAcademicYearScoped
        ? Payments.Where(p => p.Kind == PaymentKind.Receipt).Sum(p => p.AllocatedToSelectedAcademicYearCentimes)
        : Payments.Where(p => p.Kind == PaymentKind.Receipt).Sum(p => p.AmountCentimes);

    /// <summary>إجمالي المصروف — إيصالات الاسترجاع</summary>
    public long RefundsTotalCentimes => IsAcademicYearScoped
        ? 0
        : Payments.Where(p => p.Kind == PaymentKind.Refund).Sum(p => p.AmountCentimes);

    /// <summary>الرصيد القائم = Σ متبقّي المستحقات الفعّالة فقط — الملغى موثق ولا يُحسب (D-108/D-109)</summary>
    public long BalanceCentimes => Charges.Where(c => c.IsActive).Sum(c => c.RemainingCentimes);
}

/// <summary>تقرير حركة المدفوعات لفترة (6.1 — D-127): يلفّ سجل المدفوعات القائم بلا SQL جديد — الإجماليات مشتقة من الأسطر (D-109)</summary>
public sealed record PaymentMovementReportItem(DateOnly From, DateOnly To, IReadOnlyList<PaymentListItem> Rows)
{
    public int ReceiptsCount => Rows.Count(r => r.Kind == PaymentKind.Receipt);
    public long ReceiptsTotalCentimes => Rows.Where(r => r.Kind == PaymentKind.Receipt).Sum(r => r.AmountCentimes);
    public int RefundsCount => Rows.Count(r => r.Kind == PaymentKind.Refund);
    public long RefundsTotalCentimes => Rows.Where(r => r.Kind == PaymentKind.Refund).Sum(r => r.AmountCentimes);

    /// <summary>إجمالي المخصوص على كل الإيصالات المعروضة</summary>
    public long AllocatedTotalCentimes => Rows.Sum(r => r.AllocatedCentimes);

    /// <summary>إجمالي غير المخصص — القبض وحده يولّد زائدة (الصرف صفر بلا استثناء)</summary>
    public long UnallocatedTotalCentimes => Rows.Sum(r => r.UnallocatedCentimes);

    /// <summary>الصافي = قبض − صرف</summary>
    public long NetCentimes => ReceiptsTotalCentimes - RefundsTotalCentimes;
}

/// <summary>سطر دفعة خام من القاعدة (بلا وصف SQL) — الوصف يُركَّب في الـHandler (D-128)</summary>
public sealed record StudentPaymentRaw(int Id, int ReceiptNo, PaymentKind Kind, string? PayerName,
    long AmountCentimes, DateTime PaidOn, string? Note, long AllocatedCentimes);

/// <summary>سطر تخصيص خام — معرف الدفعة لتجميع السطور تحت إيصالاتها + معرف المستحق لتركيب الوصف + المبلغ (D-128)</summary>
public sealed record StudentPaymentAllocationRaw(int PaymentId, int ChargeId, long AmountCentimes);

/// <summary>رزمة قراءة واحدة: المدفوعات + كل تخصيصاتها (D-128)</summary>
public sealed record StudentPaymentsRead(
    IReadOnlyList<StudentPaymentRaw> Payments,
    IReadOnlyList<StudentPaymentAllocationRaw> Allocations);

// ═══ 6.3 — ط-2: قراءة الإيصال المفرد للطباعة (سليمان ومثبتان باختبارات نموذج الإيصال — يبقيان بلا تغيير) ═══

/// <summary>سطر تخصيص خام لإيصال مفرد — معرف المستحق + المبلغ (الوصف يُركَّب في الـHandler من مستحقات الطالب — D-128)</summary>
public sealed record ReceiptAllocationLineRaw(int ChargeId, long AmountCentimes);

/// <summary>إيصال مفرد بكل ما تلزم طباعته خاماً: الدفعة + الطالب + تخصيصاتها (6.3 — ط-2)</summary>
public sealed record ReceiptPrintRead(
    int Id, int ReceiptNo, PaymentKind Kind,
    int StudentId, string StudentName,
    string? PayerName,
    long AmountCentimes, DateTime PaidOn, string? Note,
    IReadOnlyList<ReceiptAllocationLineRaw> Allocations);

// ═══ 6.4 — ق-1: حضور الطلاب لفترة ═══

/// <summary>علامة حضور خام في حصة مُقامة ضمن فترة (D-100) — بلا تجميع SQL: التجميع في الـHandler (روح D-128)</summary>
public sealed record AttendanceMarkRaw(int StudentId, string StudentName, int ClassGroupId, string GroupName, AttendanceStatus Status);

/// <summary>سطر ملخص حضور: طالب × فوج — نسبة الحضور = حاضر ÷ (حاضر + غائب) والمبرر خارجها (اتساق D-93: لا خصم فلا عقاب)</summary>
public sealed record AttendanceSummaryItem(
    int StudentId, string StudentName, int ClassGroupId, string GroupName,
    int PresentCount, int AbsentCount, int JustifiedCount)
{
    public int MarkedCount => PresentCount + AbsentCount + JustifiedCount;

    /// <summary>«75%» أو «—» عند غياب المقسوم — نص جاهز للعرض (العدد مختبَر من العدّات)</summary>
    public string AttendancePercentText
    {
        get
        {
            var denominator = PresentCount + AbsentCount;
            return denominator == 0
                ? "—"
                : $"{(int)Math.Round(100.0 * PresentCount / denominator)}%";
        }
    }
}

/// <summary>تقرير حضور الطلاب لفترة — الأسطر + إجماليات محسوبة منها (روح D-109)</summary>
public sealed record AttendanceSummaryReportItem(DateOnly From, DateOnly To, IReadOnlyList<AttendanceSummaryItem> Rows)
{
    public int PresentTotal => Rows.Sum(r => r.PresentCount);
    public int AbsentTotal => Rows.Sum(r => r.AbsentCount);
    public int JustifiedTotal => Rows.Sum(r => r.JustifiedCount);

    /// <summary>النسبة الإجمالية على نفس قاعدة السطر (المبرر خارجها)</summary>
    public string OverallPercentText
    {
        get
        {
            var denominator = PresentTotal + AbsentTotal;
            return denominator == 0
                ? "—"
                : $"{(int)Math.Round(100.0 * PresentTotal / denominator)}%";
        }
    }
}

// ═══ 6.4 — ق-2: حصص الأفواج لفترة ═══

/// <summary>سطر ملخص فوج لفترة — مجمَّع من قراءة الحصص المسطّحة القائمة (بلا SQL جديد) · HeldMinutes لمراقبة أجور «بالساعة» (روح D-124)</summary>
public sealed record GroupSessionsSummaryItem(
    int ClassGroupId, string GroupName, string SubjectName, string LevelName, string? TeacherName,
    int ScheduledCount, int HeldCount, int CancelledCount, int HeldMinutes)
{
    /// <summary>ساعات مُقامة بصيغة «2.5» (ثقافة ثابتة — اتساق تنسيق المال D-51)</summary>
    public string HeldHoursText => (HeldMinutes / 60.0).ToString("0.#", CultureInfo.InvariantCulture);
}

/// <summary>تقرير حصص الأفواج لفترة — إجماليات مشتقة من الأسطر (روح D-109)</summary>
public sealed record GroupSessionsReportItem(DateOnly From, DateOnly To, IReadOnlyList<GroupSessionsSummaryItem> Groups)
{
    public int ScheduledTotal => Groups.Sum(g => g.ScheduledCount);
    public int HeldTotal => Groups.Sum(g => g.HeldCount);
    public int CancelledTotal => Groups.Sum(g => g.CancelledCount);
    public int HeldMinutesTotal => Groups.Sum(g => g.HeldMinutes);
    public string HeldHoursTotalText => (HeldMinutesTotal / 60.0).ToString("0.#", CultureInfo.InvariantCulture);
}

// ═══ 6.4 — ق-5: تنبيه نفاد أرصدة الحصص ═══

/// <summary>رصيد تسجيل نشط خام — تعبيرا المشتريات/المخصوم مأخوذان حرفاً من قراءة «أفواجه» القائمة (D-81: لا تعبير مكرر بصيغة أخرى)</summary>
public sealed record EnrollmentBalanceRaw(
    int EnrollmentId, int StudentId, string StudentName,
    int ClassGroupId, string GroupName, string SubjectName,
    int PurchasedSessions, int ConsumedSessions,
    string? GuardianName, string? GuardianPhone, string? StudentPhone);

/// <summary>سطر تنبيه نفاد رصيد (ق-5): الرصيد = مشترى − مخصوم والسالب مسموح مرئي (D-92) · جهة التذكير: هاتف الولي ثم هاتف الطالب (D-36)</summary>
public sealed record LowSessionBalanceItem(
    int EnrollmentId, int StudentId, string StudentName,
    int ClassGroupId, string GroupName, string SubjectName, int Balance,
    string? GuardianName, string? GuardianPhone, string? StudentPhone)
{
    public bool IsNegative => Balance < 0;

    /// <summary>هاتف التذكير المقترح — الولي أولاً ثم الطالب (الإخوة يتقاسمون ولياً واحداً — D-36)</summary>
    public string? ContactPhone => GuardianPhone ?? StudentPhone;
    public string ContactName => GuardianName ?? StudentName;
}
