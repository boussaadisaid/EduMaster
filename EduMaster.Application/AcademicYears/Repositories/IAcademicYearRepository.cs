using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;



namespace EduMaster.Application.AcademicYears.Repositories
{
    public interface IAcademicYearRepository
    {
        Task AddAsync(AcademicYear academicYear, CancellationToken cancellationToken = default);
        Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<AcademicYear?> GetCurrentAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(YearName name, CancellationToken cancellationToken = default);
        Task UpdateAsync(AcademicYear academicYear, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
