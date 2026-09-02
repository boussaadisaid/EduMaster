using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Treasury;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Treasury;

public sealed record AddTreasuryTransferRequest(int FromTreasuryAccountId, int ToTreasuryAccountId,
    DateOnly TransferDate, long AmountCentimes, string? Note);
public sealed record RemoveTreasuryTransferRequest(int TransferId);

public sealed class AddTreasuryTransferHandler
{
    private readonly ITreasuryTransferRepository _transfers;
    private readonly ITreasuryAccountRepository _accounts;
    private readonly IClock _clock; private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork; private readonly ILogger<AddTreasuryTransferHandler> _logger;
    public AddTreasuryTransferHandler(ITreasuryTransferRepository transfers, ITreasuryAccountRepository accounts,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<AddTreasuryTransferHandler> logger)
        => (_transfers, _accounts, _clock, _currentUser, _unitOfWork, _logger) = (transfers, accounts, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult<int>> ExecuteAsync(AddTreasuryTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransferDate > _clock.Today) return OperationResult<int>.Failure("تاريخ التحويل لا يمكن أن يكون في المستقبل.", ErrorType.Validation);
        try
        {
            var from = await _accounts.GetByIdAsync(request.FromTreasuryAccountId, cancellationToken);
            if (from is null) return OperationResult<int>.Failure("الحساب المصدر غير موجود.", ErrorType.NotFound);
            var to = await _accounts.GetByIdAsync(request.ToTreasuryAccountId, cancellationToken);
            if (to is null) return OperationResult<int>.Failure("الحساب المستفيد غير موجود.", ErrorType.NotFound);
            if (!from.IsActive) return OperationResult<int>.Failure("الحساب المصدر معطّل.", ErrorType.BusinessRule);
            if (!to.IsActive) return OperationResult<int>.Failure("الحساب المستفيد معطّل.", ErrorType.BusinessRule);
            var transfer = TreasuryTransfer.Create(request.FromTreasuryAccountId, request.ToTreasuryAccountId,
                request.TransferDate, request.AmountCentimes, request.Note, _clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _transfers.AddAsync(transfer, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult<int>.Success(transfer.Id);
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected AddTreasuryTransfer");
            return OperationResult<int>.Failure(ex.Message, ErrorType.Validation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to add treasury transfer");
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تنفيذ التحويل المالي.", ErrorType.Unexpected);
        }
    }
}

public sealed class RemoveTreasuryTransferHandler
{
    private readonly ITreasuryTransferRepository _transfers; private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser; private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveTreasuryTransferHandler> _logger;
    public RemoveTreasuryTransferHandler(ITreasuryTransferRepository transfers, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<RemoveTreasuryTransferHandler> logger)
        => (_transfers, _clock, _currentUser, _unitOfWork, _logger) = (transfers, clock, currentUser, unitOfWork, logger);
    public async Task<OperationResult> ExecuteAsync(RemoveTreasuryTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var transfer = await _transfers.GetByIdAsync(request.TransferId, cancellationToken);
            if (transfer is null || transfer.IsDeleted) return OperationResult.Failure("التحويل المالي غير موجود.", ErrorType.NotFound);
            transfer.SoftDelete(_clock.UtcNow, _currentUser.UserAccountId);
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _transfers.SoftDeleteAsync(request.TransferId, _clock.UtcNow, _currentUser.UserAccountId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DomainException ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogWarning(ex, "Domain rule rejected RemoveTreasuryTransfer {Id}", request.TransferId);
            return OperationResult.Failure(ex.Message, ErrorType.Validation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken); _logger.LogError(ex, "Failed to remove treasury transfer {Id}", request.TransferId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حذف التحويل المالي.", ErrorType.Unexpected);
        }
    }
}
