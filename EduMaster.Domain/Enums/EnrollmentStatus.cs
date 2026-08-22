namespace EduMaster.Domain.Enums;

/// <summary>حالة التسجيل (D-53): نشط/منسحب فقط — النقل والعودة عمليات بصفوف جديدة وليست حالات</summary>
public enum EnrollmentStatus : byte
{
    Active = 1,
    Withdrawn = 2
}