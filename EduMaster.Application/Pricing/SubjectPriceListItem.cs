namespace EduMaster.Application.Pricing;

/// <summary>نموذج قراءة مسطّح لأسعار المواد (D-40) — السعر بالسنتيم، والعرض بالدينار عبر محوّل الواجهة (D-51/D-67)</summary>
public sealed record SubjectPriceListItem(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    int LevelId,
    string LevelName,
    int SubjectId,
    string SubjectName,
    long UnitPriceCentimes);