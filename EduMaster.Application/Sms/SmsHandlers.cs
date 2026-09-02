using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Sms;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Sms;

public sealed class GetSmsSettingsHandler
{
    private readonly ISmsSettingsStore _store;
    public GetSmsSettingsHandler(ISmsSettingsStore store) => _store = store;
    public async Task<OperationResult<SmsGatewaySettings>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try { return OperationResult<SmsGatewaySettings>.Success(await _store.GetAsync(cancellationToken)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult<SmsGatewaySettings>.Failure("تعذّر تحميل إعدادات SMS.", ErrorType.Unexpected); }
    }
}

public sealed record SaveSmsSettingsRequest(string? ApiKey, string? DeviceId);

public sealed class SaveSmsSettingsHandler
{
    private readonly ISmsSettingsStore _store;
    public SaveSmsSettingsHandler(ISmsSettingsStore store) => _store = store;

    public async Task<OperationResult> ExecuteAsync(SaveSmsSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ApiKey)) return OperationResult.Failure("مفتاح API مطلوب.", ErrorType.Validation);
        if (request.ApiKey.Trim().Length > 500) return OperationResult.Failure("مفتاح API غير صالح.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(request.DeviceId)) return OperationResult.Failure("يجب اختيار جهاز الإرسال.", ErrorType.Validation);
        if (request.DeviceId.Trim().Length > 100) return OperationResult.Failure("معرف الجهاز غير صالح.", ErrorType.Validation);
        try
        {
            await _store.SaveAsync(new SmsGatewaySettings(request.ApiKey.Trim(), request.DeviceId.Trim()), cancellationToken);
            return OperationResult.Success();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.Failure("تعذّر حفظ إعدادات SMS.", ErrorType.Unexpected); }
    }
}

public sealed class ClearSmsSettingsHandler
{
    private readonly ISmsSettingsStore _store;
    public ClearSmsSettingsHandler(ISmsSettingsStore store) => _store = store;
    public async Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try { await _store.ClearAsync(cancellationToken); return OperationResult.Success(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.Failure("تعذّر مسح إعدادات SMS.", ErrorType.Unexpected); }
    }
}

public sealed class GetSmsDevicesHandler
{
    private readonly ISmsProvider _provider;
    public GetSmsDevicesHandler(ISmsProvider provider) => _provider = provider;
    public async Task<OperationResult<IReadOnlyList<SmsProviderDevice>>> ExecuteAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return OperationResult<IReadOnlyList<SmsProviderDevice>>.Failure("أدخل مفتاح TextBee API أولاً.", ErrorType.Validation);
        try { return OperationResult<IReadOnlyList<SmsProviderDevice>>.Success(await _provider.GetDevicesAsync(apiKey.Trim(), cancellationToken)); }
        catch (SmsProviderException ex) { return OperationResult<IReadOnlyList<SmsProviderDevice>>.Failure(ex.UserMessage, ex.ErrorType); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult<IReadOnlyList<SmsProviderDevice>>.Failure("تعذّر الاتصال بخدمة SMS.", ErrorType.Unexpected); }
    }
}

public sealed record GetSmsTemplatesRequest(bool ActiveOnly);

public sealed class GetSmsTemplatesHandler
{
    private readonly ISmsTemplateRepository _repo;
    public GetSmsTemplatesHandler(ISmsTemplateRepository repo) => _repo = repo;
    public async Task<OperationResult<IReadOnlyList<SmsTemplateItem>>> ExecuteAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _repo.GetAllAsync(activeOnly, cancellationToken);
            return OperationResult<IReadOnlyList<SmsTemplateItem>>.Success(
                items.Select(x => new SmsTemplateItem(x.Id, x.Name, x.Category, x.Body, x.IsActive)).ToList());
        }
        catch (Exception) { return OperationResult<IReadOnlyList<SmsTemplateItem>>.Failure("تعذّر تحميل قوالب SMS.", ErrorType.Unexpected); }
    }
}

public sealed record CreateSmsTemplateRequest(string Name, SmsMessageCategory Category, string Body);
public sealed record UpdateSmsTemplateRequest(int Id, string Name, SmsMessageCategory Category, string Body);
public sealed record SetSmsTemplateActiveRequest(int Id);

public sealed class CreateSmsTemplateHandler
{
    private readonly ISmsTemplateRepository _repo;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CreateSmsTemplateHandler> _logger;
    public CreateSmsTemplateHandler(ISmsTemplateRepository repo, IClock clock, ICurrentUserService currentUser, IUnitOfWork uow, ILogger<CreateSmsTemplateHandler> logger)
    { _repo = repo; _clock = clock; _currentUser = currentUser; _uow = uow; _logger = logger; }
    public async Task<OperationResult<int>> ExecuteAsync(CreateSmsTemplateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (await _repo.AnyWithNameAsync(request.Name, null, ct))
                return OperationResult<int>.Failure("يوجد قالب بهذا الاسم بالفعل.", ErrorType.Conflict);
            var item = SmsTemplate.Create(request.Name, request.Category, request.Body, _clock.UtcNow, _currentUser.UserAccountId);
            await _uow.BeginTransactionAsync(ct); await _repo.AddAsync(item, ct); await _uow.CommitAsync(ct);
            return OperationResult<int>.Success(item.Id);
        }
        catch (DomainException ex) { await _uow.RollbackAsync(ct); _logger.LogWarning(ex, "SMS template creation rejected"); return OperationResult<int>.Failure(ex.Message, ErrorType.Validation); }
        catch (Exception ex) { await _uow.RollbackAsync(ct); _logger.LogError(ex, "SMS template creation failed"); return OperationResult<int>.Failure("تعذّر إنشاء قالب SMS.", ErrorType.Unexpected); }
    }
}

public sealed class UpdateSmsTemplateHandler
{
    private readonly ISmsTemplateRepository _repo;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateSmsTemplateHandler> _logger;
    public UpdateSmsTemplateHandler(ISmsTemplateRepository repo, IClock clock, ICurrentUserService currentUser, IUnitOfWork uow, ILogger<UpdateSmsTemplateHandler> logger)
    { _repo = repo; _clock = clock; _currentUser = currentUser; _uow = uow; _logger = logger; }
    public async Task<OperationResult> ExecuteAsync(UpdateSmsTemplateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var item = await _repo.GetByIdAsync(request.Id, ct);
            if (item is null) return OperationResult.Failure("القالب غير موجود.", ErrorType.NotFound);
            if (await _repo.AnyWithNameAsync(request.Name, request.Id, ct)) return OperationResult.Failure("يوجد قالب بهذا الاسم بالفعل.", ErrorType.Conflict);
            item.Update(request.Name, request.Category, request.Body, _clock.UtcNow, _currentUser.UserAccountId);
            await _uow.BeginTransactionAsync(ct); await _repo.UpdateAsync(item, ct); await _uow.CommitAsync(ct);
            return OperationResult.Success();
        }
        catch (DomainException ex) { await _uow.RollbackAsync(ct); _logger.LogWarning(ex, "SMS template update rejected"); return OperationResult.Failure(ex.Message, ErrorType.Validation); }
        catch (Exception ex) { await _uow.RollbackAsync(ct); _logger.LogError(ex, "SMS template update failed"); return OperationResult.Failure("تعذّر تعديل قالب SMS.", ErrorType.Unexpected); }
    }
}

public sealed class SetSmsTemplateActiveHandler
{
    private readonly ISmsTemplateRepository _repo;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    public SetSmsTemplateActiveHandler(ISmsTemplateRepository repo, IClock clock, ICurrentUserService currentUser, IUnitOfWork uow)
    { _repo = repo; _clock = clock; _currentUser = currentUser; _uow = uow; }
    public async Task<OperationResult> ExecuteAsync(SetSmsTemplateActiveRequest request, bool active, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        if (item is null) return OperationResult.Failure("القالب غير موجود.", ErrorType.NotFound);
        try
        {
            if (active) item.Activate(_clock.UtcNow, _currentUser.UserAccountId); else item.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);
            await _uow.BeginTransactionAsync(ct); await _repo.UpdateAsync(item, ct); await _uow.CommitAsync(ct); return OperationResult.Success();
        }
        catch (DomainException ex) { await _uow.RollbackAsync(ct); return OperationResult.Failure(ex.Message, ErrorType.Validation); }
        catch (Exception) { await _uow.RollbackAsync(ct); return OperationResult.Failure("تعذّر تحديث حالة القالب.", ErrorType.Unexpected); }
    }
}

public sealed class SendSmsBatchHandler
{
    private readonly ISmsRepository _repo;
    private readonly ISmsProvider _provider;
    private readonly ISmsSettingsStore _settings;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SendSmsBatchHandler> _logger;

    public SendSmsBatchHandler(ISmsRepository repo, ISmsProvider provider, ISmsSettingsStore settings, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork uow, ILogger<SendSmsBatchHandler> logger)
    { _repo = repo; _provider = provider; _settings = settings; _clock = clock; _currentUser = currentUser; _uow = uow; _logger = logger; }

    public async Task<OperationResult<SendSmsResult>> ExecuteAsync(SendSmsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Recipients is null || request.Recipients.Count == 0)
            return OperationResult<SendSmsResult>.Failure("اختر مستلماً واحداً على الأقل.", ErrorType.Validation);

        var settings = await _settings.GetAsync(ct);
        if (!settings.IsConfigured)
            return OperationResult<SendSmsResult>.Failure("أكمل إعداد خدمة SMS واختر جهاز الإرسال أولاً.", ErrorType.BusinessRule);

        try
        {
            var now = _clock.UtcNow;
            var batch = SmsBatch.Create(request.Category, request.TemplateId, settings.DeviceId, request.Recipients.Count,
                now, _currentUser.UserAccountId);

            await _uow.BeginTransactionAsync(ct);
            await _repo.AddBatchAsync(batch, ct);
            var messages = new List<SmsMessage>(request.Recipients.Count);
            foreach (var recipient in request.Recipients)
            {
                var message = SmsMessage.Create(batch.Id, recipient.PersonId, recipient.StudentId,
                    recipient.PhoneNumber, recipient.Message, request.TemplateId, now);
                await _repo.AddMessageAsync(message, ct);
                messages.Add(message);
            }
            await _uow.CommitAsync(ct);

            SmsProviderSendResult providerResult;
            try
            {
                providerResult = await _provider.SendBulkAsync(
                    messages.Select(m => new SmsProviderMessage(m.Id, m.PhoneNumber, m.MessageBody)).ToList(),
                    settings.DeviceId!, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TextBee SMS send failed for local batch {BatchId}", batch.Id);
                await MarkProviderFailureAsync(batch, messages, ex.Message, ct);
                return OperationResult<SendSmsResult>.Failure("تعذّر إرسال الرسائل عبر خدمة SMS.", ErrorType.Unexpected);
            }

            if (!providerResult.Accepted)
            {
                await MarkProviderFailureAsync(batch, messages, providerResult.ErrorMessage, ct);
                return OperationResult<SendSmsResult>.Failure(providerResult.ErrorMessage ?? "رفضت خدمة SMS عملية الإرسال.", ErrorType.BusinessRule);
            }

            foreach (var message in messages) message.MarkSubmitted(now);
            batch.SetProviderBatchId(providerResult.ProviderBatchId);
            batch.Recalculate(messages.Count, 0, 0, now);

            await _uow.BeginTransactionAsync(ct);
            foreach (var message in messages) await _repo.UpdateMessageAsync(message, ct);
            await _repo.UpdateBatchAsync(batch, ct);
            await _uow.CommitAsync(ct);

            return OperationResult<SendSmsResult>.Success(new SendSmsResult(batch.Id, messages.Count, batch.ProviderBatchId));
        }
        catch (DomainException ex)
        {
            await _uow.RollbackAsync(ct);
            return OperationResult<SendSmsResult>.Failure(ex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to create/send SMS batch");
            return OperationResult<SendSmsResult>.Failure("تعذّر إنشاء عملية إرسال SMS.", ErrorType.Unexpected);
        }
    }

    private async Task MarkProviderFailureAsync(SmsBatch batch, IReadOnlyList<SmsMessage> messages, string? error, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        foreach (var message in messages) message.MarkFailed(now, "provider_error", null, error);
        batch.MarkFailed();
        await _uow.BeginTransactionAsync(ct);
        foreach (var message in messages) await _repo.UpdateMessageAsync(message, ct);
        await _repo.UpdateBatchAsync(batch, ct);
        await _uow.CommitAsync(ct);
    }
}

public sealed class SyncSmsBatchHandler
{
    private readonly ISmsRepository _repo;
    private readonly ISmsProvider _provider;
    private readonly ISmsSettingsStore _settings;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;

    public SyncSmsBatchHandler(ISmsRepository repo, ISmsProvider provider, ISmsSettingsStore settings, IClock clock, IUnitOfWork uow)
    { _repo = repo; _provider = provider; _settings = settings; _clock = clock; _uow = uow; }

    public async Task<OperationResult> ExecuteAsync(int batchId, CancellationToken ct = default)
    {
        var batch = await _repo.GetBatchByIdAsync(batchId, ct);
        if (batch is null) return OperationResult.Failure("دفعة الرسائل غير موجودة.", ErrorType.NotFound);
        if (string.IsNullOrWhiteSpace(batch.ProviderBatchId) || string.IsNullOrWhiteSpace(batch.DeviceId))
            return OperationResult.Success();
        try
        {
            var providerBatch = await _provider.GetBatchAsync(batch.DeviceId!, batch.ProviderBatchId!, ct);
            var localMessages = await _repo.GetMessagesByBatchIdAsync(batchId, ct);
            var now = _clock.UtcNow;

            var changedMessages = new List<SmsMessage>();
            foreach (var local in localMessages)
            {
                var external = providerBatch.Messages.FirstOrDefault(x =>
                    string.Equals(x.ProviderMessageId, local.ProviderMessageId, StringComparison.OrdinalIgnoreCase) ||
                    (local.ProviderMessageId is null && string.Equals(x.Recipient, local.PhoneNumber, StringComparison.OrdinalIgnoreCase)));
                if (external is null) continue;

                var oldStatus = local.Status;
                var oldProviderStatus = local.ProviderStatus;
                switch (external.Status.ToLowerInvariant())
                {
                    case "delivered":
                        local.MarkDelivered(external.DeliveredAtUtc ?? now, external.ProviderMessageId, external.Status);
                        break;
                    case "sent":
                    case "dispatched":
                        local.MarkSent(external.SentAtUtc ?? now, external.ProviderMessageId, external.Status);
                        break;
                    case "failed":
                        local.MarkFailed(external.FailedAtUtc ?? now, external.Status, external.ErrorCode, external.ErrorMessage);
                        break;
                    default:
                        local.MarkSubmitted(now, external.Status);
                        break;
                }

                if (oldStatus != local.Status || !string.Equals(oldProviderStatus, local.ProviderStatus, StringComparison.Ordinal))
                    changedMessages.Add(local);
            }

            var submitted = localMessages.Count(x => x.Status is SmsMessageStatus.Submitted or SmsMessageStatus.Delivered);
            var delivered = localMessages.Count(x => x.Status == SmsMessageStatus.Delivered);
            var failed = localMessages.Count(x => x.Status == SmsMessageStatus.Failed);
            batch.Recalculate(submitted, delivered, failed, now);

            await _uow.BeginTransactionAsync(ct);
            foreach (var local in changedMessages) await _repo.UpdateMessageAsync(local, ct);
            await _repo.UpdateBatchAsync(batch, ct);
            foreach (var local in changedMessages)
            {
                // لا نكرر الحدث في كل polling إذا لم تتغير الحالة.
                await _repo.AddDeliveryEventAsync(SmsDeliveryEvent.Create(local.Id, local.Status, local.ProviderStatus,
                    local.ProviderErrorCode, now, null), ct);
            }
            await _uow.CommitAsync(ct);
            return OperationResult.Success();
        }
        catch (SmsProviderException ex) { return OperationResult.Failure(ex.UserMessage, ex.ErrorType); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { await _uow.RollbackAsync(ct); return OperationResult.Failure("تعذّرت مزامنة حالات الرسائل.", ErrorType.Unexpected); }
    }
}

public sealed class GetSmsHistoryHandler
{
    private readonly ISmsRepository _repo;
    public GetSmsHistoryHandler(ISmsRepository repo) => _repo = repo;
    public async Task<OperationResult<IReadOnlyList<SmsHistoryItem>>> ExecuteAsync(DateTime? fromUtc = null, DateTime? toUtc = null,
        int? status = null, int? category = null, string? search = null, CancellationToken ct = default)
    {
        try { return OperationResult<IReadOnlyList<SmsHistoryItem>>.Success(await _repo.GetHistoryAsync(fromUtc, toUtc, status, category, search, ct)); }
        catch (Exception) { return OperationResult<IReadOnlyList<SmsHistoryItem>>.Failure("تعذّر تحميل سجل SMS.", ErrorType.Unexpected); }
    }
}

public sealed class SmsProviderException : Exception
{
    public string UserMessage { get; }
    public ErrorType ErrorType { get; }
    public SmsProviderException(string userMessage, ErrorType errorType = ErrorType.Unexpected, Exception? inner = null)
        : base(userMessage, inner) { UserMessage = userMessage; ErrorType = errorType; }
}
