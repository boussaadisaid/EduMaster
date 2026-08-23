using EduMaster.Application.Scheduling;
using EduMaster.Domain.Scheduling;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IClassSessionRepository
{
    Task AddAsync(ClassSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClassSession session, CancellationToken cancellationToken = default);
    Task<ClassSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>حصص فترة (من from شامل إلى toExclusive حصرياً) — مسطّحة بأسماء الفوج/المادة/المستوى/الأستاذ/القاعة + عدد النشطين</summary>
    Task<IEnumerable<ClassSessionListItem>> GetByDateRangeAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default);
    /// <summary>فرادة التوقيت الودية (D-87) — الفهرس الفريد يحمي قاعدةً</summary>
    Task<bool> AnyExistsAtAsync(int classGroupId, DateTime startsAt, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>بدايات حصص فوج في فترة — لإسقاط المكرر أثناء التوليد (آمن لإعادة الضغط)</summary>
    Task<IReadOnlyCollection<DateTime>> GetSessionStartsAsync(int classGroupId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default);
    /// <summary>D-88: إلغاء جماعي للحصص المستقبلية المجدولة لموعد (StartsAt > localNow · Status=1) — يعيد عددها</summary>
    Task<int> CancelFutureScheduledBySlotAsync(int scheduleId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>D-90: إلغاء جماعي للحصص المستقبلية المجدولة لفوج عند تعطيله — يعيد عددها</summary>
    Task<int> CancelFutureScheduledByGroupAsync(int classGroupId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default);
}