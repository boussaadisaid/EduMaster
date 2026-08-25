namespace EduMaster.Domain.Enums;

/// <summary>حالة المستحق — لا حذف مالياً إطلاقاً (D-109): الإلغاء حالة موثقة بسبب (D-108)</summary>
public enum ChargeStatus : byte
{
    Active = 1,
    Cancelled = 2
}