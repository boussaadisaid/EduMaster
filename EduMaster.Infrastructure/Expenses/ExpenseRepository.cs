using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Expenses;
using EduMaster.Domain.Expenses;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Expenses;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly IAdoDbSession _session;
    public ExpenseRepository(IAdoDbSession session) => _session = session;

    private sealed record ExpenseRow(int Id, int AcademicYearId, int ExpenseCategoryId, DateTime ExpenseDate,
        long AmountCentimes, string? Note, bool IsDeleted, DateTime CreatedAtUtc, int? CreatedByUserId,
        DateTime? UpdatedAtUtc, int? UpdatedByUserId, DateTime? DeletedAtUtc, int? DeletedByUserId);
    private sealed record ListRow(int Id, int AcademicYearId, string AcademicYearName, int ExpenseCategoryId,
        string CategoryName, DateTime ExpenseDate, long AmountCentimes, string? Note, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"INSERT INTO dbo.Expenses
(AcademicYearId, ExpenseCategoryId, ExpenseDate, AmountCentimes, Note, IsDeleted, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@AcademicYearId, @ExpenseCategoryId, @ExpenseDate, @AmountCentimes, @Note, 0, @CreatedAtUtc, @CreatedByUserId);";
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            expense.AcademicYearId,
            expense.ExpenseCategoryId,
            ExpenseDate = expense.ExpenseDate.ToDateTime(TimeOnly.MinValue),
            expense.AmountCentimes,
            expense.Note,
            expense.CreatedAtUtc,
            expense.CreatedByUserId
        }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        expense.SetId(id);
    }

    public async Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"UPDATE dbo.Expenses
SET AcademicYearId=@AcademicYearId, ExpenseCategoryId=@ExpenseCategoryId, ExpenseDate=@ExpenseDate,
    AmountCentimes=@AmountCentimes, Note=@Note, UpdatedAtUtc=@UpdatedAtUtc, UpdatedByUserId=@UpdatedByUserId
WHERE Id=@Id AND IsDeleted=0;";
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            expense.AcademicYearId,
            expense.ExpenseCategoryId,
            ExpenseDate = expense.ExpenseDate.ToDateTime(TimeOnly.MinValue),
            expense.AmountCentimes,
            expense.Note,
            expense.UpdatedAtUtc,
            expense.UpdatedByUserId,
            expense.Id
        }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"Expense {expense.Id} was not found for update.");
    }

    public async Task<Expense?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"SELECT Id, AcademicYearId, ExpenseCategoryId, ExpenseDate, AmountCentimes, Note, IsDeleted,
CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId, DeletedAtUtc, DeletedByUserId
FROM dbo.Expenses WHERE Id=@Id;";
        var row = await connection.QuerySingleOrDefaultAsync<ExpenseRow>(new CommandDefinition(sql, new { Id = id },
            transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return row is null ? null : Expense.Load(row.Id, row.AcademicYearId, row.ExpenseCategoryId,
            DateOnly.FromDateTime(row.ExpenseDate), row.AmountCentimes, row.Note, row.IsDeleted,
            row.CreatedAtUtc, row.CreatedByUserId, row.UpdatedAtUtc, row.UpdatedByUserId,
            row.DeletedAtUtc, row.DeletedByUserId);
    }

    public async Task<IReadOnlyList<ExpenseListItem>> GetForPeriodAsync(int academicYearId, DateOnly? from, DateOnly? to,
        int? categoryId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"SELECT e.Id, e.AcademicYearId, y.Name AS AcademicYearName,
 e.ExpenseCategoryId, c.Name AS CategoryName, e.ExpenseDate, e.AmountCentimes, e.Note,
 e.CreatedAtUtc, e.UpdatedAtUtc
FROM dbo.Expenses e
INNER JOIN dbo.AcademicYears y ON y.Id=e.AcademicYearId
INNER JOIN dbo.ExpenseCategories c ON c.Id=e.ExpenseCategoryId
WHERE e.IsDeleted=0 AND e.AcademicYearId=@AcademicYearId
  AND (@From IS NULL OR e.ExpenseDate>=@From)
  AND (@To IS NULL OR e.ExpenseDate<=@To)
  AND (@CategoryId IS NULL OR e.ExpenseCategoryId=@CategoryId)
ORDER BY e.ExpenseDate DESC, e.Id DESC;";
        var rows = await connection.QueryAsync<ListRow>(new CommandDefinition(sql, new
        {
            AcademicYearId = academicYearId,
            From = from?.ToDateTime(TimeOnly.MinValue),
            To = to?.ToDateTime(TimeOnly.MinValue),
            CategoryId = categoryId
        }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.Select(r => new ExpenseListItem(r.Id, r.AcademicYearId, r.AcademicYearName, r.ExpenseCategoryId,
            r.CategoryName, DateOnly.FromDateTime(r.ExpenseDate), r.AmountCentimes, r.Note, r.CreatedAtUtc, r.UpdatedAtUtc)).ToList();
    }

    public async Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"UPDATE dbo.Expenses
SET IsDeleted=1, DeletedAtUtc=@DeletedAtUtc, DeletedByUserId=@DeletedByUserId
WHERE Id=@Id AND IsDeleted=0;", new { Id = id, DeletedAtUtc = deletedAtUtc, DeletedByUserId = deletedByUserId },
            transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"Expense {id} was not found for soft delete.");
    }
}
