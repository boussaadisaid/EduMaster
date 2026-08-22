using EduMaster.Application.ClassGroups;
using EduMaster.Domain.ClassGroups;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IClassGroupRepository
{
    Task AddAsync(ClassGroup classGroup, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClassGroup classGroup, CancellationToken cancellationToken = default);
    Task<ClassGroup?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>فرادة اسم الفوج داخل السنة الواحدة — excludeId لاستثناء الذات عند التعديل (نمط D-27)</summary>
    Task<bool> AnyWithNameInYearAsync(int academicYearId, string name, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>نموذج قراءة مسطّح (JOIN السنة/المستوى/المادة/الأستاذ/القاعة + تجميع الشعب) — المصطلح يصل مطبَّعاً من الـHandler</summary>
    Task<IEnumerable<ClassGroupListItem>> SearchAsync(int? academicYearId, string? normalizedTerm, CancellationToken cancellationToken = default);
    /// <summary>معرفات شعب الفوج الحالية — القائمة الفارغة تعني: يقبل كل شعب المستوى (D-48)</summary>
    Task<IReadOnlyList<int>> GetStreamIdsAsync(int classGroupId, CancellationToken cancellationToken = default);
    /// <summary>استبدال ذرّي لشعب الفوج داخل معاملة الـHandler — LevelId يُمرَّر لدعامة الـFK المركّب (D-48)</summary>
    Task ReplaceStreamsAsync(int classGroupId, int levelId, IReadOnlyList<int> streamIds, CancellationToken cancellationToken = default);
    /// <summary>2.4: يُملأ بالتسجيلات (ClassGroupEnrollments) — اليوم لا جداول تشير إلى ClassGroups</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
}