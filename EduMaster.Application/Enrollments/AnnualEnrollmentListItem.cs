using EduMaster.Domain.Enums;

namespace EduMaster.Application.Enrollments;

/// <summary>نموذج قراءة مسطّح لتسجيلات طالب (D-40) — الحقوق بالسنتيم والعرض بالدينار عبر محوّل الواجهة (D-51)</summary>
public sealed record AnnualEnrollmentListItem(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    int LevelId,
    string LevelName,
    int? StreamId,
    string? StreamName,
    EnrollmentStatus Status,
    long AgreedRegistrationFeeCentimes,
    string? RegistrationFeeNote,
    DateTime EnrolledAtUtc,
    DateTime? WithdrawnAtUtc)
{
    public string StatusText => Status == EnrollmentStatus.Active ? "نشط" : "منسحب";
    public string StreamDisplay => StreamName ?? "—";
}