namespace EduMaster.Domain.Payroll
{
    /// <summary>
    /// نوع سياسة الأجر (D-113): 1..3 للأساتذة · 4..5 للموظفين ·
    /// «ثابت للحصة» و«بالأسبوع» مؤجلان بلا كسر — يُضافان عند أول حالة فعلية.
    /// </summary>
    public enum PayPolicyKind : byte
    {
        PerPresentStudent = 1,
        Percentage = 2,
        PerHour = 3,
        PerDay = 4,
        PerMonth = 5
    }
}