using EduMaster.Domain.Academic;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IStreamRepository
{
    Task AddAsync(Domain.Academic.Stream stream, CancellationToken cancellationToken = default);
    Task UpdateAsync(Domain.Academic.Stream stream, CancellationToken cancellationToken = default);
    Task<Domain.Academic.Stream?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>شعب مستوى معيّن (فعّالة ومعطّلة) مرتبة بالاسم — Master-Detail في شاشة الإدارة</summary>
    Task<IReadOnlyList<Domain.Academic.Stream>> GetByLevelIdAsync(int levelId, CancellationToken cancellationToken = default);
    /// <summary>فرادة الاسم داخل المستوى الواحد (لا عموماً) — excludeId لاستثناء الذات عند التعديل</summary>
    Task<bool> AnyWithNameInLevelAsync(int levelId, string name, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>ح-5: يُملأ في F2 — اليوم لا جداول تشير إلى Streams</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
}