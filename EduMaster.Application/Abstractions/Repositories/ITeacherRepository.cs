using EduMaster.Application.Teachers;
using EduMaster.Domain.Teachers;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ITeacherRepository
{
    Task AddAsync(Teacher teacher, CancellationToken cancellationToken = default);
    Task UpdateAsync(Teacher teacher, CancellationToken cancellationToken = default);
    Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeacherListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default);
    Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default);
}