using EduMaster.Domain.Academic;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ISubjectRepository
{
    Task AddAsync(Subject subject, CancellationToken cancellationToken = default);
    Task UpdateAsync(Subject subject, CancellationToken cancellationToken = default);
    Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>كل المواد (فعّالة ومعطّلة) مرتبة بالاسم</summary>
    Task<IReadOnlyList<Subject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>ح-5: يُملأ في F2 — اليوم لا جداول تشير إلى Subjects</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
}