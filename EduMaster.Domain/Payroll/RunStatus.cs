namespace EduMaster.Domain.Payroll;

/// <summary>حالة كشف الأجور (D-116): مسودة قابلة لإعادة الحساب والحذف ← معتمد يقفل نهائياً (لا تعديل ولا حذف — الخطأ بعده يُصحَّح بصرف تسوية).</summary>
public enum RunStatus
{
    Draft = 1,     // مسودة
    Approved = 2,  // معتمد
}