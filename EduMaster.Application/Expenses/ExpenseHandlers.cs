using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Expenses;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Expenses;

public sealed class GetExpensesHandler
{
    private readonly IExpenseRepository _expenses;
    private readonly ILogger<GetExpensesHandler> _logger;
    public GetExpensesHandler(IExpenseRepository expenses, ILogger<GetExpensesHandler> logger)
        => (_expenses, _logger) = (expenses, logger);
    public async Task<OperationResult<IReadOnlyList<ExpenseListItem>>> ExecuteAsync(int academicYearId, DateOnly? from, DateOnly? to,
        int? categoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (academicYearId <= 0)
                return OperationResult<IReadOnlyList<ExpenseListItem>>.Failure("السنة الدراسية غير صالحة.", ErrorType.Validation);
            if (from is not null && to is not null && from > to)
                return OperationResult<IReadOnlyList<ExpenseListItem>>.Failure("تاريخ البداية يجب أن يكون قبل تاريخ النهاية.", ErrorType.Validation);
            return OperationResult<IReadOnlyList<ExpenseListItem>>.Success(
                await _expenses.GetForPeriodAsync(academicYearId, from, to, categoryId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expenses for academic year {AcademicYearId}", academicYearId);
            return OperationResult<IReadOnlyList<ExpenseListItem>>.Failure("تعذّر تحميل المصاريف.", ErrorType.Unexpected);
        }
    }
}

public sealed record AddExpenseRequest(int AcademicYearId, int ExpenseCategoryId, int TreasuryAccountId, DateOnly ExpenseDate, long AmountCentimes, string? Note);
public sealed class AddExpenseHandler
{
    private readonly IExpenseRepository _expenses; private readonly IExpenseCategoryRepository _categories;
    private readonly IAcademicYearRepository _years; private readonly ITreasuryAccountRepository _treasuryAccounts; private readonly IClock _clock; private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork; private readonly ILogger<AddExpenseHandler> _logger;
    public AddExpenseHandler(IExpenseRepository expenses, IExpenseCategoryRepository categories, IAcademicYearRepository years, ITreasuryAccountRepository treasuryAccounts,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<AddExpenseHandler> logger)
    {
        _expenses = expenses; _categories = categories; _years = years; _treasuryAccounts = treasuryAccounts;
        _clock = clock; _currentUser = currentUser; _unitOfWork = unitOfWork; _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(AddExpenseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _years.GetByIdAsync(request.AcademicYearId, cancellationToken) is null)
                return OperationResult<int>.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);
            var account = await _treasuryAccounts.GetByIdAsync(request.TreasuryAccountId, cancellationToken);
            if (account is null) return OperationResult<int>.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!account.IsActive) return OperationResult<int>.Failure("الحساب المالي معطّل.", ErrorType.BusinessRule);
            var category = await _categories.GetByIdAsync(request.ExpenseCategoryId, cancellationToken);
            if (category is null) return OperationResult<int>.Failure("فئة المصروف غير موجودة.", ErrorType.NotFound);
            if (!category.IsActive) return OperationResult<int>.Failure("فئة المصروف معطّلة.", ErrorType.BusinessRule);
            var expense = Expense.Create(request.AcademicYearId, request.ExpenseCategoryId, request.TreasuryAccountId, request.ExpenseDate,
                request.AmountCentimes, request.Note, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _expenses.AddAsync(expense, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult<int>.Success(expense.Id);
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected AddExpense");
            return OperationResult<int>.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to add expense");
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تسجيل المصروف.", ErrorType.Unexpected);
        }
    }
}

public sealed record UpdateExpenseRequest(int ExpenseId, int AcademicYearId, int ExpenseCategoryId, int TreasuryAccountId, DateOnly ExpenseDate, long AmountCentimes, string? Note);
public sealed class UpdateExpenseHandler
{
    private readonly IExpenseRepository _expenses; private readonly IExpenseCategoryRepository _categories;
    private readonly IAcademicYearRepository _years; private readonly ITreasuryAccountRepository _treasuryAccounts; private readonly IClock _clock; private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork; private readonly ILogger<UpdateExpenseHandler> _logger;
    public UpdateExpenseHandler(IExpenseRepository expenses, IExpenseCategoryRepository categories, IAcademicYearRepository years, ITreasuryAccountRepository treasuryAccounts,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<UpdateExpenseHandler> logger)
    {
        _expenses = expenses; _categories = categories; _years = years; _treasuryAccounts = treasuryAccounts;
        _clock = clock; _currentUser = currentUser; _unitOfWork = unitOfWork; _logger = logger;
    }
    public async Task<OperationResult> ExecuteAsync(UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var expense = await _expenses.GetByIdAsync(request.ExpenseId, cancellationToken);
            if (expense is null || expense.IsDeleted) return OperationResult.Failure("المصروف غير موجود.", ErrorType.NotFound);
            if (await _years.GetByIdAsync(request.AcademicYearId, cancellationToken) is null)
                return OperationResult.Failure("السنة الدراسية غير موجودة.", ErrorType.NotFound);
            var account = await _treasuryAccounts.GetByIdAsync(request.TreasuryAccountId, cancellationToken);
            if (account is null) return OperationResult.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!account.IsActive && account.Id != expense.TreasuryAccountId)
                return OperationResult.Failure("لا يمكن نقل المصروف إلى حساب مالي معطّل.", ErrorType.BusinessRule);
            var category = await _categories.GetByIdAsync(request.ExpenseCategoryId, cancellationToken);
            if (category is null) return OperationResult.Failure("فئة المصروف غير موجودة.", ErrorType.NotFound);
            if (!category.IsActive && category.Id != expense.ExpenseCategoryId)
                return OperationResult.Failure("لا يمكن نقل المصروف إلى فئة معطّلة.", ErrorType.BusinessRule);
            expense.Update(request.AcademicYearId, request.ExpenseCategoryId, request.TreasuryAccountId, request.ExpenseDate,
                request.AmountCentimes, request.Note, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _expenses.UpdateAsync(expense, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to update expense {Id}", request.ExpenseId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل المصروف.", ErrorType.Unexpected);
        }
    }
}

public sealed record RemoveExpenseRequest(int ExpenseId);
public sealed class RemoveExpenseHandler
{
    private readonly IExpenseRepository _expenses; private readonly IClock _clock; private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork; private readonly ILogger<RemoveExpenseHandler> _logger;
    public RemoveExpenseHandler(IExpenseRepository expenses, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<RemoveExpenseHandler> logger)
        => (_expenses, _clock, _currentUser, _unitOfWork, _logger) = (expenses, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(RemoveExpenseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var expense = await _expenses.GetByIdAsync(request.ExpenseId, cancellationToken);
            if (expense is null || expense.IsDeleted) return OperationResult.Failure("المصروف غير موجود.", ErrorType.NotFound);
            expense.SoftDelete(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _expenses.SoftDeleteAsync(request.ExpenseId, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to remove expense {Id}", request.ExpenseId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف المصروف.", ErrorType.Unexpected);
        }
    }
}
