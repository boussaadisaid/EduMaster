using EduMaster.Application.Employees;
using EduMaster.Domain.Employees;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IEmployeeRepository
{
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
    Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default);
    Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>ملف موظف فعّال واحد لكل شخص — الفهرس المفلتر يضمن القاعدة، وهذا الفحص يعطي الرسالة النظيفة (D-22/نمط D-39)</summary>
    Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default);
    /// <summary>نموذج قراءة مسطّح بربط Persons (D-40) — المصطلح يصل مطبَّعاً من الـHandler — null = كل الموظفين</summary>
    Task<IEnumerable<EmployeeListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default);
    /// <summary>حارس الإزالة: سياسة أجر أو يوم عمل مسجَّل على الملف يمنع إزالته (بروح D-109 — وتُضاف الأرصدة في 5.3)</summary>
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default);
}