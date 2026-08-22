using EduMaster.Domain.Enums;

namespace EduMaster.Application.Enrollments;

/// <summary>قسم «أفواجه» في لوحة الطالب (D-75) — نموذج قراءة مسطّح (D-40)</summary>
public sealed record StudentGroupEnrollmentItem(
    int Id,
    int ClassGroupId,
    string ClassGroupName,
    string SubjectName,
    string AcademicYearName,
    EnrollmentStatus Status,
    long AgreedUnitPriceCentimes,
    DateTime EnrolledAtUtc)
{
    public string StatusText => Status == EnrollmentStatus.Active ? "نشط" : "منسحب";
}