using EduMaster.Application.Scheduling;
using EduMaster.Domain.Scheduling;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IClassGroupScheduleRepository
{
    Task AddAsync(ClassGroupSchedule schedule, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClassGroupSchedule schedule, CancellationToken cancellationToken = default);
    Task<ClassGroupSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>المواعيد الفعّالة لأفواج فعّالة — كل الجدول (null) أو فوج واحد — تغذي التوليد (D-87)</summary>
    Task<IEnumerable<ScheduleSlotItem>> GetActiveAsync(int? classGroupId, CancellationToken cancellationToken = default);
    /// <summary>شاشة جدول استعمال الزمن: الفعّالة افتراضياً، والمعطّلة تشملها عند الطلب لتُفعَّل منها (D-86) — لأفواج فعّالة فقط</summary>
    Task<IEnumerable<ScheduleSlotItem>> GetForTimetableAsync(bool includeInactive, CancellationToken cancellationToken = default);
    /// <summary>كل مواعيد فوج (فعّالة ومعطّلة) — لإدارتها</summary>
    Task<IEnumerable<ScheduleSlotItem>> GetForGroupAsync(int classGroupId, CancellationToken cancellationToken = default);
    /// <summary>تعارضات قاعة/أستاذ في نفس اليوم والمدى الزمني المتقاطع — تحذير غير مانع (D-89) عبر قراءة مسبقة</summary>
    Task<IEnumerable<ScheduleConflictItem>> FindConflictsAsync(int dayOfWeek, TimeSpan startTime, int durationMinutes,
        int? roomId, int? teacherId, int? excludeId, CancellationToken cancellationToken = default);
}