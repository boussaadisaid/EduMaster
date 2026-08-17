using EduMaster.Domain.People;



namespace EduMaster.Application.Abstractions.Repositories;

public interface IPersonRepository
{
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
}