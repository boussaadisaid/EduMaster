using EduMaster.Domain.Enums;

namespace EduMaster.Application.Enrollments;

/// <summary>
/// قسم «أفواجه» في لوحة الطالب (D-75) — نموذج قراءة مسطّح (D-40)
/// · F3: مشتريات + نقل داخل/خارج + مخصوم من الحضور والرصيد محسوب منها
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

    /// <summary>الرصيد = مشتريات + نقل داخل − نقل خارج − مخصوم — السالب مسموح ويُلوَّن أحمر في الواجهة.</summary>
    public int TransferredInSessions { get; init; }
    public int TransferredOutSessions { get; init; }

    public int Balance => PurchasedSessions + TransferredInSessions - TransferredOutSessions - ConsumedSessions;

    /// <summary>علامة تجاوز الرصيد (D-92) — تلوين الواجهة</summary>
    public bool IsNegativeBalance => Balance < 0;
}