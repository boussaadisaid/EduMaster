using EduMaster.Domain.People;



namespace EduMaster.Application.Abstractions.Repositories;

public interface IPersonRepository
{
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);
    Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>المصطلح يصل مطبَّعاً من الـHandler عبر ArabicTextNormalizer — null = كل الأشخاص</summary>
    Task<IEnumerable<Person>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default);
}