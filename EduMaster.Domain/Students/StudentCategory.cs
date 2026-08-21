namespace EduMaster.Domain.Students;

/// <summary>صنف الطالب — هوية علاقته بالمدرسة (شبه ثابتة، تُعدَّل بحرية). التفاصيل الأكاديمية السنوية (مستوى/شعبة/فوج) تأتي عبر التسجيل في F2</summary>
public enum StudentCategory : byte
{
    Regular = 1,        // نظامي
    FreeCandidate = 2,  // مترشح حر
    University = 3,     // جامعي
    Training = 4,       // تكوين ودورات
}