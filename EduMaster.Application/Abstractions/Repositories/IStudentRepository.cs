using EduMaster.Application.Students;
using EduMaster.Domain.Students;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IStudentRepository
{
    Task AddAsync(Student student, CancellationToken cancellationToken = default);
    Task UpdateAsync(Student student, CancellationToken cancellationToken = default);
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default);
    /// <summary>نموذج قراءة مسطّح (JOIN مع النواة وولي الأمر) — المصطلح يصل مطبَّعاً من الـHandler</summary>
    Task<IEnumerable<StudentListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default);
    /// <summary>ح-7: يُملأ في F2 (التسجيلات) — اليوم لا جداول تشير إلى Students</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>حذف منطقي — التدقيق يُمرَّر من الـHandler (D-20)</summary>
    Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default);
}