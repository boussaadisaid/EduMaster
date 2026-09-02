using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Treasury;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Treasury;

public sealed record AddTreasuryTransactionRequest(int TreasuryAccountId, DateOnly TransactionDate,
    TreasuryTransactionKind Kind, long AmountCentimes, string? Note);
public sealed record UpdateTreasuryTransactionRequest(int TransactionId, int TreasuryAccountId, DateOnly TransactionDate,
    TreasuryTransactionKind Kind, long AmountCentimes, string? Note);
public sealed record RemoveTreasuryTransactionRequest(int TransactionId);

public sealed class AddTreasuryTransactionHandler
{
    private readonly ITreasuryTransactionRepository _transactions;
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock; private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork; private readonly ILogger<AddTreasuryTransactionHandler> _logger;
    public AddTreasuryTransactionHandler(ITreasuryTransactionRepository transactions, ITreasuryAccountRepository accounts,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<AddTreasuryTransactionHandler> logger)
        => (_transactions, _accounts, _clock, _currentUser, _unitOfWork, _logger) = (transactions, accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult<int>> ExecuteAsync(AddTreasuryTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransactionDate > _clock.Today) return OperationResult<int>.Failure("تاريخ الحركة لا يمكن أن يكون في المستقبل.", ErrorType.Validation);
        try
        {
            var account = await _accounts.GetByIdAsync(request.TreasuryAccountId, cancellationToken);
            if (account is null) return OperationResult<int>.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!account.IsActive) return OperationResult<int>.Failure("الحساب المالي معطّل.", ErrorType.BusinessRule);
            var transaction = TreasuryTransaction.Create(request.TreasuryAccountId, request.TransactionDate, request.Kind,
                request.AmountCentimes, request.Note, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _transactions.AddAsync(transaction, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult<int>.Success(transaction.Id);
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected AddTreasuryTransaction");
            return OperationResult<int>.Failure(ex.Message, ErrorType.Validation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to add treasury transaction");
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تسجيل الحركة المالية.", ErrorType.Unexpected);
        }
    }
}

public sealed class UpdateTreasuryTransactionHandler
{
    private readonly ITreasuryTransactionRepository _transactions;
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock; private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork; private readonly ILogger<UpdateTreasuryTransactionHandler> _logger;
    public UpdateTreasuryTransactionHandler(ITreasuryTransactionRepository transactions, ITreasuryAccountRepository accounts,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<UpdateTreasuryTransactionHandler> logger)
        => (_transactions, _accounts, _clock, _currentUser, _unitOfWork, _logger) = (transactions, accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(UpdateTreasuryTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransactionDate > _clock.Today) return OperationResult.Failure("تاريخ الحركة لا يمكن أن يكون في المستقبل.", ErrorType.Validation);
        try
        {
            var transaction = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken);
            if (transaction is null || transaction.IsDeleted) return OperationResult.Failure("الحركة المالية غير موجودة.", ErrorType.NotFound);
            var account = await _accounts.GetByIdAsync(request.TreasuryAccountId, cancellationToken);
            if (account is null) return OperationResult.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!account.IsActive && account.Id != transaction.TreasuryAccountId)
                return OperationResult.Failure("لا يمكن نقل الحركة إلى حساب مالي معطّل.", ErrorType.BusinessRule);
            transaction.Update(request.TreasuryAccountId, request.TransactionDate, request.Kind, request.AmountCentimes,
                request.Note, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _transactions.UpdateAsync(transaction, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected UpdateTreasuryTransaction {Id}", request.TransactionId);
            return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to update treasury transaction {Id}", request.TransactionId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الحركة المالية.", ErrorType.Unexpected);
        }
    }
}

public sealed class RemoveTreasuryTransactionHandler
{
    private readonly ITreasuryTransactionRepository _transactions; private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser; private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveTreasuryTransactionHandler> _logger;
    public RemoveTreasuryTransactionHandler(ITreasuryTransactionRepository transactions, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<RemoveTreasuryTransactionHandler> logger)
        => (_transactions, _clock, _currentUser, _unitOfWork, _logger) = (transactions, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(RemoveTreasuryTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var transaction = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken);
            if (transaction is null || transaction.IsDeleted) return OperationResult.Failure("الحركة المالية غير موجودة.", ErrorType.NotFound);
            transaction.SoftDelete(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _transactions.SoftDeleteAsync(request.TransactionId, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected RemoveTreasuryTransaction {Id}", request.TransactionId);
            return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to remove treasury transaction {Id}", request.TransactionId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف الحركة المالية.", ErrorType.Unexpected);
        }
    }
}

public sealed class GetTreasuryMovementsHandler
{
    private readonly ITreasuryReadRepository _read;
    private readonly ILogger<GetTreasuryMovementsHandler> _logger;

    public GetTreasuryMovementsHandler(
        ITreasuryReadRepository read,
        ILogger<GetTreasuryMovementsHandler> logger)
    {
        _read = read;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<TreasuryMovementItem>>> ExecuteAsync(
        int? treasuryAccountId, DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        if (from is not null && to is not null && from > to)
        {
            return OperationResult<IReadOnlyList<TreasuryMovementItem>>.Failure(
                "تاريخ البداية يجب أن يكون قبل تاريخ النهاية.", ErrorType.Validation);
        }

        try
        {
            var movements = await _read.GetMovementsAsync(
                treasuryAccountId, from, to, cancellationToken);

            return OperationResult<IReadOnlyList<TreasuryMovementItem>>.Success(movements);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load treasury movements for account {TreasuryAccountId}",
                treasuryAccountId);

            return OperationResult<IReadOnlyList<TreasuryMovementItem>>.Failure(
                "تعذّر تحميل حركات الخزينة.", ErrorType.Unexpected);
        }
    }
}

public sealed class GetTreasurySummaryHandler
{
    private readonly ITreasuryReadRepository _read;
    private readonly ILogger<GetTreasurySummaryHandler> _logger;
    public GetTreasurySummaryHandler(ITreasuryReadRepository read, ILogger<GetTreasurySummaryHandler> logger) => (_read, _logger) = (read, logger);
    public async Task<OperationResult<TreasurySummaryItem>> ExecuteAsync(int? treasuryAccountId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to) return OperationResult<TreasurySummaryItem>.Failure("تاريخ البداية يجب أن يكون قبل تاريخ النهاية.", ErrorType.Validation);
        try { return OperationResult<TreasurySummaryItem>.Success(await _read.GetSummaryAsync(treasuryAccountId, from, to, cancellationToken)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "Failed to load treasury summary for {TreasuryAccountId}", treasuryAccountId); return OperationResult<TreasurySummaryItem>.Failure("تعذّر تحميل ملخص الخزينة.", ErrorType.Unexpected); }
    }
}
