using EduMaster.Domain.Expenses;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IExpenseCategoryRepository
{
    Task AddAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<ExpenseCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseCategory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);
}
