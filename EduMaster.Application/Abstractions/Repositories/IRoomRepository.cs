using EduMaster.Domain.Academic;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IRoomRepository
{
    Task AddAsync(Room room, CancellationToken cancellationToken = default);
    Task UpdateAsync(Room room, CancellationToken cancellationToken = default);
    Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>كل القاعات (فعّالة ومعطّلة) مرتبة بالاسم</summary>
    Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>ح-5: يُملأ في F2 — اليوم لا جداول تشير إلى Rooms</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
}