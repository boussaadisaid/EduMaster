using EduMaster.Domain.Enums;

namespace EduMaster.Application.Enrollments;

/// <summary>
/// قسم «أفواجه» في لوحة الطالب (D-75) — نموذج قراءة مسطّح (D-40)
/// · F3: مشترى/مخصوم من جدولَي المشتريات والحضور (المخصوم 0 حتى 3.3 — عموده جاهز) والرصيد محسوب (D-91/D-98)
/// </summary>
public sealed record StudentGroupEnrollmentItem(
    int Id,
    int ClassGroupId,
    string ClassGroupName,
    string SubjectName,
    string AcademicYearName,
    EnrollmentStatus Status,
    long AgreedUnitPriceCentimes,
    DateTime EnrolledAtUtc,
    int PurchasedSessions,
    int ConsumedSessions)
{
    public string StatusText => Status == EnrollmentStatus.Active ? "نشط" : "منسحب";

    /// <summary>الرصيد = مشترى − مخصوم — السالب مسموح ويُلوَّن أحمر في الواجهة (D-92)</summary>
    public int Balance => PurchasedSessions - ConsumedSessions;

    /// <summary>علامة تجاوز الرصيد (D-92) — تلوين الواجهة</summary>
    public bool IsNegativeBalance => Balance < 0;
}