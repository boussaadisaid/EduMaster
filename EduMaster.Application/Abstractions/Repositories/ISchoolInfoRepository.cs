using EduMaster.Domain.Settings;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>هوية المدرسة (ط-7/D-130) — جدول صف واحد (Id=1): قراءة + إنشاء أولي + تحديث</summary>
public interface ISchoolInfoRepository
{
    /// <summary>الصف الوحيد إن وُجد — null إن لم تُهيَّأ بعد (018 تبذره، والقراءة تتحصّن من الغياب دفاعاً)</summary>
    Task<SchoolInfo?> GetAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SchoolInfo info, CancellationToken cancellationToken = default);
    Task UpdateAsync(SchoolInfo info, CancellationToken cancellationToken = default);
}