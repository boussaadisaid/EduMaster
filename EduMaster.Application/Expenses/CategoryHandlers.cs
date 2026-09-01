using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Expenses;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Expenses;

public sealed class GetExpenseCategoriesHandler
{
    private readonly IExpenseCategoryRepository _categories;
    public GetExpenseCategoriesHandler(IExpenseCategoryRepository categories) => _categories = categories;

    public async Task<OperationResult<IReadOnlyList<ExpenseCategoryItem>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _categories.GetAllAsync(cancellationToken))
                .Select(c => new ExpenseCategoryItem(c.Id, c.Name, c.IsActive)).ToList();
            return OperationResult<IReadOnlyList<ExpenseCategoryItem>>.Success(items);
        }
        catch (Exception)
        {
            return OperationResult<IReadOnlyList<ExpenseCategoryItem>>.Failure(
                "تعذّر تحميل فئات المصاريف.", ErrorType.Unexpected);
        }
    }
}

public sealed record CreateExpenseCategoryRequest(string Name);
public sealed class CreateExpenseCategoryHandler
{
    private readonly IExpenseCategoryRepository _categories; private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser; private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateExpenseCategoryHandler> _logger;
    public CreateExpenseCategoryHandler(IExpenseCategoryRepository categories, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateExpenseCategoryHandler> logger)
        => (_categories, _clock, _currentUser, _unitOfWork, _logger) = (categories, clock, currentUser, unitOfWork, logger);

    public async Task<OperationResult<int>> ExecuteAsync(CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (await _categories.AnyWithNameAsync(request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("توجد فئة بنفس الاسم.", ErrorType.Conflict);
            var category = ExpenseCategory.Create(request.Name, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _categories.AddAsync(category, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult<int>.Success(category.Id);
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Domain rule rejected CreateExpenseCategory");
            return OperationResult<int>.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create expense category");
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء الفئة.", ErrorType.Unexpected);
        }
    }
}

public sealed record UpdateExpenseCategoryRequest(int Id, string Name);
public sealed class UpdateExpenseCategoryHandler
{
    private readonly IExpenseCategoryRepository _categories; private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser; private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateExpenseCategoryHandler> _logger;
    public UpdateExpenseCategoryHandler(IExpenseCategoryRepository categories, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateExpenseCategoryHandler> logger)
        => (_categories, _clock, _currentUser, _unitOfWork, _logger) = (categories, clock, currentUser, unitOfWork, logger);

    public async Task<OperationResult> ExecuteAsync(UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var category = await _categories.GetByIdAsync(request.Id, cancellationToken);
            if (category is null) return OperationResult.Failure("الفئة غير موجودة.", ErrorType.NotFound);
            if (await _categories.AnyWithNameAsync(request.Name.Trim(), request.Id, cancellationToken))
                return OperationResult.Failure("توجد فئة بنفس الاسم.", ErrorType.Conflict);
            category.Update(request.Name, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _categories.UpdateAsync(category, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to update expense category {Id}", request.Id);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الفئة.", ErrorType.Unexpected);
        }
    }
}

public sealed record DeactivateExpenseCategoryRequest(int Id);
public sealed class DeactivateExpenseCategoryHandler
{
    private readonly IExpenseCategoryRepository _categories; private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser; private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateExpenseCategoryHandler> _logger;
    public DeactivateExpenseCategoryHandler(IExpenseCategoryRepository categories, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<DeactivateExpenseCategoryHandler> logger)
        => (_categories, _clock, _currentUser, _unitOfWork, _logger) = (categories, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(DeactivateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var category = await _categories.GetByIdAsync(request.Id, cancellationToken);
            if (category is null) return OperationResult.Failure("الفئة غير موجودة.", ErrorType.NotFound);
            category.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _categories.UpdateAsync(category, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to deactivate expense category {Id}", request.Id);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل الفئة.", ErrorType.Unexpected);
        }
    }
}

public sealed record ActivateExpenseCategoryRequest(int Id);
public sealed class ActivateExpenseCategoryHandler
{
    private readonly IExpenseCategoryRepository _categories; private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser; private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateExpenseCategoryHandler> _logger;
    public ActivateExpenseCategoryHandler(IExpenseCategoryRepository categories, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateExpenseCategoryHandler> logger)
        => (_categories, _clock, _currentUser, _unitOfWork, _logger) = (categories, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(ActivateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var category = await _categories.GetByIdAsync(request.Id, cancellationToken);
            if (category is null) return OperationResult.Failure("الفئة غير موجودة.", ErrorType.NotFound);
            category.Activate(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _categories.UpdateAsync(category, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to activate expense category {Id}", request.Id);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل الفئة.", ErrorType.Unexpected);
        }
    }
}
