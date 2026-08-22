


namespace EduMaster.Application.AcademicYears.UpdateAcademicYear;

/// <summary>حقوق التسجيل بالسنتيم (D-51) — 0 = بلا حقوق (D-66)</summary>
public sealed record UpdateAcademicYearRequest
    (int Id, string? Name, DateOnly StartDate, DateOnly EndDate, long RegistrationFeeCentimes);