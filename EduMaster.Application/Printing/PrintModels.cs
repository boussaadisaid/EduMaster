using EduMaster.Application.Reports;    // PaymentMovementReportItem + StudentStatementItem
using EduMaster.Application.Students;   // StudentListItem
using EduMaster.Domain.Enums;           // PaymentKind

namespace EduMaster.Application.Printing;

/// <summary>
/// ترويسة هوية المدرسة على المطبوعات (ط-7/D-130) — تُبنى مرة وتُلحق بكل وثيقة ·
/// LogoPath = اسم الملف المخزَّن، والمسار الكامل يُحل عبر IImageStore في الواجهة عند الرسم (D-38)
/// </summary>
public sealed record PrintHeader(string SchoolName, string? SchoolPhone, string? SchoolAddress, string? LogoPath);

/// <summary>سطر تخصيص على الإيصال المطبوع</summary>
public sealed record ReceiptAllocationPrintLine(string SourceDescription, long AmountCentimes);

/// <summary>
/// نموذج إيصال جاهز للرسم (ط-4/ط-6) — أرقامه بالسنتيم وتنسيق الدينار يقع على المرسّم في الواجهة (D-51) ·
/// مرآة الصرف تلقائية من Kind — لا نموذج ثانٍ (ط-3)
/// </summary>
public sealed record ReceiptPrintModel(
    PrintHeader Header,
    PaymentKind Kind,
    int ReceiptNo,
    DateTime PaidOn,
    string StudentName,
    string? PayerName,
    long AmountCentimes,
    string? Note,
    IReadOnlyList<ReceiptAllocationPrintLine> Allocations)
{
    public string DocumentTitle => Kind == PaymentKind.Receipt ? "إيصال قبض" : "إيصال صرف (استرجاع)";
    public string ReceiptNoText => $"#{ReceiptNo:000000}";
    public bool IsRefund => Kind == PaymentKind.Refund;
    public bool HasAllocations => Allocations.Count > 0;

    /// <summary>ما بقي من مبلغ هذه الدفعة بلا تخصيص (الصرف لا تخصيص له)</summary>
    public long UnallocatedCentimes => IsRefund ? 0 : AmountCentimes - Allocations.Sum(a => a.AmountCentimes);
    public bool HasUnallocated => UnallocatedCentimes > 0;
}

/// <summary>طباعة تقرير الحركة (6.1) — تلتقط البيانات المعروضة حرفياً: الإجماليات من مصدرها المختبَر بلا إعادة حساب (WYSIWYP)</summary>
public sealed record PaymentMovementPrintModel(PrintHeader Header, PaymentMovementReportItem Report);

/// <summary>طباعة كشف حساب الطالب (6.1) — بيانات الطالب من الشاشة نفسها (StudentListItem — لا استعلام جديد)</summary>
public sealed record StudentStatementPrintModel(PrintHeader Header, StudentListItem Student, StudentStatementItem Statement);

// ═══ 6.4 — ق-6: التقرير الجدولي العام ═══

/// <summary>عمود تقرير جدولي بوزن نسبي — توزيع البكسلات على عرض المحتوى شأن المرسّم وحده (قاعدة البكسل المطلق من 6.3 تبقى عليه)</summary>
public sealed record TabularReportColumn(string Header, double Weight);

/// <summary>
/// نموذج تقرير جدولي A4 عام (6.4 — ق-6): ترويسة + عنوان + سطر وصفي + سطر إجماليات + جدول واحد ·
/// القيم تصل منسَّقة نصياً من الشاشة حرفياً (WYSIWYP — التنسيق في الـVM كما سطر الملخص القائم) ·
/// نموذج واحد تتقاسمه تقارير 6.4 الخمسة بدل خمسة مرسّمات مكررة — والمرسّم يبقى قابلاً للاستبدال وحده (D-130)
/// </summary>
public sealed record TabularReportPrintModel(
    PrintHeader Header,
    string Title,
    string? SubtitleLine,
    string? TotalsLine,
    IReadOnlyList<TabularReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
