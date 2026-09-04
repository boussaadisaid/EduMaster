using EduMaster.Application.Scheduling;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>الحقيقة الوحيدة لحساب رصيد الحصص لتسجيل فوج.</summary>
public interface ISessionBalanceRepository
{
    Task<SessionBalanceSnapshot?> GetAsync(int classGroupEnrollmentId, CancellationToken cancellationToken = default);
}
