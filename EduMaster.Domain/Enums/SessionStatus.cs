namespace EduMaster.Domain.Enums;

/// <summary>حالة الحصة (D-90): مجدولة ← مُقامة (تفتح الحضور) أو ملغاة (لا تخصم) — لا حذف، والتاريخ يُحفظ بالحالة</summary>
public enum SessionStatus : byte
{
    Scheduled = 1,
    Held = 2,
    Cancelled = 3
}