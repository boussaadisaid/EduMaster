namespace EduMaster.Domain.Payroll
{
    /// <summary>المستفيد من الأجر (D-116): 1 أستاذ · 2 موظف</summary>
    public enum PayeeKind : byte
    {
        Teacher = 1,
        Employee = 2
    }
}