using EduMaster.Domain.Academic;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ILevelRepository
{
    Task AddAsync(Level level, CancellationToken cancellationToken = default);
    Task UpdateAsync(Level level, CancellationToken cancellationToken = default);
    Task<Level?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>كل المستويات (فعّالة ومعطّلة — شاشة الإدارة) مرتبة بـSortOrder ثم الاسم</summary>
    Task<IReadOnlyList<Level>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>فحص فرادة الاسم الودي قبل الاصطدام بالقيد (D-22) — excludeId لاستثناء الذات عند التعديل</summary>
    Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>ح-5: يُملأ في F2 (الأفواج ستشير إلى المستويات) — اليوم لا جداول تشير إلى Levels</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
}