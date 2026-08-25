namespace EduMaster.Domain.Payroll;

/// <summary>مصدر سطر الكشف (D-123/س-8): محسوب من المحرك (تسقطه إعادة الحساب الذرّية) · يدوي مكافأة/خصم بسبب إلزامي (ينجو منها).</summary>
public enum LineSourceKind
{
    Computed = 1,  // محسوب من المحرك
    Manual = 2,    // يدوي — مكافأة (+) أو خصم (−)
}