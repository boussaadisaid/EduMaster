using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Treasury;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Treasury;

public sealed record CreateTreasuryAccountRequest(string Name, long OpeningBalanceCentimes);
public sealed record UpdateTreasuryAccountRequest(int Id, string Name, long OpeningBalanceCentimes);
public sealed record SetTreasuryAccountActiveRequest(int Id);

public sealed class GetTreasuryAccountsHandler
{
    private readonly ITreasuryAccountRepository _accounts;
    private readonly ILogger<GetTreasuryAccountsHandler> _logger;

    public GetTreasuryAccountsHandler(
        ITreasuryAccountRepository accounts,
        ILogger<GetTreasuryAccountsHandler> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<TreasuryAccountItem>>> ExecuteAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await _accounts.GetAllAsync(activeOnly, cancellationToken);
            var items = accounts
                .Select(a => new TreasuryAccountItem(a.Id, a.Name, a.IsActive, a.OpeningBalanceCentimes))
                .ToList();

            return OperationResult<IReadOnlyList<TreasuryAccountItem>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load treasury accounts. ActiveOnly={ActiveOnly}", activeOnly);
            return OperationResult<IReadOnlyList<TreasuryAccountItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل الحسابات المالية.", ErrorType.Unexpected);
        }
    }
}

public sealed class CreateTreasuryAccountHandler
{
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateTreasuryAccountHandler> _logger;
    public CreateTreasuryAccountHandler(ITreasuryAccountRepository accounts, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<CreateTreasuryAccountHandler> logger)
        => (_accounts, _clock, _currentUser, _unitOfWork, _logger) = (accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult<int>> ExecuteAsync(CreateTreasuryAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (await _accounts.AnyWithNameAsync(request.Name.Trim(), null, cancellationToken))
                return OperationResult<int>.Failure("يوجد حساب مالي بنفس الاسم.", ErrorType.Conflict);
            var account = TreasuryAccount.Create(request.Name, request.OpeningBalanceCentimes, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _accounts.AddAsync(account, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult<int>.Success(account.Id);
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected CreateTreasuryAccount");
            return OperationResult<int>.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to create treasury account");
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء إنشاء الحساب المالي.", ErrorType.Unexpected);
        }
    }
}

public sealed class UpdateTreasuryAccountHandler
{
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateTreasuryAccountHandler> _logger;
    public UpdateTreasuryAccountHandler(ITreasuryAccountRepository accounts, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateTreasuryAccountHandler> logger)
        => (_accounts, _clock, _currentUser, _unitOfWork, _logger) = (accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(UpdateTreasuryAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var account = await _accounts.GetByIdAsync(request.Id, cancellationToken);
            if (account is null) return OperationResult.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (await _accounts.AnyWithNameAsync(request.Name.Trim(), request.Id, cancellationToken))
                return OperationResult.Failure("يوجد حساب مالي بنفس الاسم.", ErrorType.Conflict);
            account.Update(request.Name, request.OpeningBalanceCentimes, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _accounts.UpdateAsync(account, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected UpdateTreasuryAccount {Id}", request.Id);
            return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to update treasury account {Id}", request.Id);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل الحساب المالي.", ErrorType.Unexpected);
        }
    }
}

public sealed class DeactivateTreasuryAccountHandler
{
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateTreasuryAccountHandler> _logger;
    public DeactivateTreasuryAccountHandler(ITreasuryAccountRepository accounts, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<DeactivateTreasuryAccountHandler> logger)
        => (_accounts, _clock, _currentUser, _unitOfWork, _logger) = (accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(SetTreasuryAccountActiveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var account = await _accounts.GetByIdAsync(request.Id, cancellationToken);
            if (account is null) return OperationResult.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (!account.IsActive) return OperationResult.Success();
            account.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _accounts.UpdateAsync(account, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to deactivate treasury account {Id}", request.Id);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل الحساب المالي.", ErrorType.Unexpected);
        }
    }
}

public sealed class ActivateTreasuryAccountHandler
{
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateTreasuryAccountHandler> _logger;
    public ActivateTreasuryAccountHandler(ITreasuryAccountRepository accounts, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<ActivateTreasuryAccountHandler> logger)
        => (_accounts, _clock, _currentUser, _unitOfWork, _logger) = (accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(SetTreasuryAccountActiveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var account = await _accounts.GetByIdAsync(request.Id, cancellationToken);
            if (account is null) return OperationResult.Failure("الحساب المالي غير موجود.", ErrorType.NotFound);
            if (account.IsActive) return OperationResult.Success();
            account.Activate(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _accounts.UpdateAsync(account, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to activate treasury account {Id}", request.Id);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تفعيل الحساب المالي.", ErrorType.Unexpected);
        }
    }
}
