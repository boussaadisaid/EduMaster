using EduMaster.Domain.AcademicYears;

namespace EduMaster.Application.Abstractions.Repositories
{
    public interface IAcademicYearRepository
    {
        Task AddAsync(AcademicYear academicYear, CancellationToken cancellationToken = default);
        Task UpdateAsync(AcademicYear academicYear, CancellationToken cancellationToken = default);
        Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<AcademicYear?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<AcademicYear>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> AnyWithNameAsync(string name, int excludeId, CancellationToken cancellationToken = default);
        Task<bool> AnyOverlappingAsync(DateOnly startDate, DateOnly endDate, int excludeId, CancellationToken cancellationToken = default);
        Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default);
    }
}