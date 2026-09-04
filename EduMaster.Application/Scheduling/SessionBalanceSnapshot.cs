namespace EduMaster.Application.Scheduling;

/// <summary>لقطة رصيد الحصص لتسجيل فوج — مشتريات + نقل داخل − نقل خارج − مخصوم من الحضور.</summary>
public sealed record SessionBalanceSnapshot(
    int PurchasedSessions,
    int TransferredInSessions,
    int TransferredOutSessions,
    int ConsumedSessions)
{
    public int Balance => PurchasedSessions + TransferredInSessions - TransferredOutSessions - ConsumedSessions;
}
