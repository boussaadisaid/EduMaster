using EduMaster.Domain.Scheduling;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>حركات نقل رصيد الحصص — append-only.</summary>
public interface IGroupSessionTransferRepository
{
    Task AddAsync(GroupSessionTransfer transfer, CancellationToken cancellationToken = default);
}
