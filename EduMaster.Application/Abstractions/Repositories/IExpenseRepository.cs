using EduMaster.Application.Expenses;
using EduMaster.Domain.Expenses;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IExpenseRepository
{
    Task AddAsync(Expense expense, CancellationToken cancellationToken = default);
    Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<Expense?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseListItem>> GetForPeriodAsync(int academicYearId, DateOnly? from, DateOnly? to,
        int? categoryId, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default);
}
