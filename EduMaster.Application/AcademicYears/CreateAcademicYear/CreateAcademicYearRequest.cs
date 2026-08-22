

namespace EduMaster.Application.AcademicYears.CreateAcademicYear;

/// <summary>حقوق التسجيل بالسنتيم (D-51) — 0 = بلا حقوق (D-66)</summary>
public sealed record CreateAcademicYearRequest
    (string? Name, DateOnly StartDate, DateOnly EndDate, long RegistrationFeeCentimes);