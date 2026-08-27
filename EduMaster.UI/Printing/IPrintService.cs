using EduMaster.Application.Printing;

namespace EduMaster.UI.Printing;

/// <summary>نتيجة محاولة الطباعة — إلغاء المستخدم من نافذة الطباعة ليس خطأً (روح D-64)</summary>
public enum PrintOutcome { Printed, Cancelled, Failed }

/// <summary>
/// خدمة الطباعة في الواجهة (6.3 — ط-د/D-130): نموذج طباعة نقي مختبَر ← مرسّم FlowDocument ← PrintDialog
/// (المستخدم يختار الطابعة أو «Microsoft Print to PDF») ← PrintDocument.
/// صفر حزم جديدة · عديمة الحالة (Singleton) — النافذة تُنشأ لكل عملية · المرسّم يُجرَّب يدوياً بالعين.
/// </summary>
public interface IPrintService
{
    /// <summary>إيصال قبض/صرف — A5 عمودي (نصف ورقة، إيصالات المكتب كثيرة — ط-8)</summary>
    PrintOutcome PrintReceipt(ReceiptPrintModel model);

    /// <summary>تقرير حركة القبض لفترة — A4 عمودي</summary>
    PrintOutcome PrintPaymentMovement(PaymentMovementPrintModel model);

    /// <summary>كشف حساب طالب — A4 عمودي</summary>
    PrintOutcome PrintStudentStatement(StudentStatementPrintModel model);

    /// <summary>تقرير جدولي عام — A4 عمودي (6.4 — ق-6: نموذج واحد تتقاسمه تقارير 6.4 الخمسة)</summary>
    PrintOutcome PrintA4Report(TabularReportPrintModel model);
}
