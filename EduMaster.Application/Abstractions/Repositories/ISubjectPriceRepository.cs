using EduMaster.Application.Pricing;
using EduMaster.Domain.Pricing;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ISubjectPriceRepository
{
    Task AddAsync(SubjectPrice subjectPrice, CancellationToken cancellationToken = default);
    Task UpdateAsync(SubjectPrice subjectPrice, CancellationToken cancellationToken = default);
    /// <summary>حذف فيزيائي حر (D-65) — لا أحد يشير إلى الأسعار، والنسخ اللحظية تحفظ التاريخ</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<SubjectPrice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>فرادة (السنة، المستوى، المادة) — excludeId لاستثناء الذات عند التعديل</summary>
    Task<bool> AnyExistsAsync(int academicYearId, int levelId, int subjectId, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>نموذج قراءة مسطّح بأسماء السنة/المستوى/المادة (D-40) — فلتر السنة اختياري (null = كل السنوات)</summary>
    Task<IEnumerable<SubjectPriceListItem>> GetByYearAsync(int? academicYearId, CancellationToken cancellationToken = default);
}