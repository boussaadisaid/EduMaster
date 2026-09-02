using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Sms;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Sms;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Sms;

public sealed class SmsRepository : ISmsRepository
{
    private readonly IAdoDbSession _session;
    public SmsRepository(IAdoDbSession session) => _session = session;

    private sealed record BatchRow(int Id, byte Category, int? TemplateId, string? ProviderBatchId, string? DeviceId, byte Status,
        int TotalCount, int SubmittedCount, int DeliveredCount, int FailedCount, DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? LastSyncedAtUtc);
    private sealed record MessageRow(int Id, int BatchId, int? PersonId, int? StudentId, string PhoneNumber, string MessageBody, int? TemplateId,
        byte Status, string? ProviderMessageId, string? ProviderStatus, string? ProviderErrorCode, DateTime CreatedAtUtc,
        DateTime? SubmittedAtUtc, DateTime? SentAtUtc, DateTime? DeliveredAtUtc, DateTime? FailedAtUtc, string? LastErrorMessage, int RetryCount);

    public async Task AddBatchAsync(SmsBatch batch, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO dbo.SmsBatches(Category,TemplateId,ProviderBatchId,DeviceId,Status,TotalCount,SubmittedCount,DeliveredCount,FailedCount,CreatedAtUtc,CreatedByUserId,LastSyncedAtUtc) OUTPUT INSERTED.Id VALUES(@Category,@TemplateId,NULL,@DeviceId,@Status,@TotalCount,0,0,0,@CreatedAtUtc,@CreatedByUserId,NULL);", new { Category = (byte)batch.Category, batch.TemplateId, batch.DeviceId, Status = (byte)batch.Status, batch.TotalCount, batch.CreatedAtUtc, batch.CreatedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        batch.SetId(id);
    }

    public async Task AddMessageAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO dbo.SmsMessages(BatchId,PersonId,StudentId,PhoneNumber,MessageBody,TemplateId,Status,CreatedAtUtc,RetryCount) OUTPUT INSERTED.Id VALUES(@BatchId,@PersonId,@StudentId,@PhoneNumber,@MessageBody,@TemplateId,@Status,@CreatedAtUtc,0);", new { message.BatchId, message.PersonId, message.StudentId, message.PhoneNumber, message.MessageBody, message.TemplateId, Status = (byte)message.Status, message.CreatedAtUtc }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        message.SetId(id);
    }

    public async Task AddDeliveryEventAsync(SmsDeliveryEvent deliveryEvent, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO dbo.SmsDeliveryEvents(SmsMessageId,Status,ProviderStatus,ProviderErrorCode,OccurredAtUtc,RawPayload) OUTPUT INSERTED.Id VALUES(@SmsMessageId,@Status,@ProviderStatus,@ProviderErrorCode,@OccurredAtUtc,@RawPayload);", new { deliveryEvent.SmsMessageId, Status = (byte)deliveryEvent.Status, deliveryEvent.ProviderStatus, deliveryEvent.ProviderErrorCode, deliveryEvent.OccurredAtUtc, deliveryEvent.RawPayload }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        deliveryEvent.SetId(id);
    }

    public async Task<SmsBatch?> GetBatchByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var r = await c.QuerySingleOrDefaultAsync<BatchRow>(new CommandDefinition(@"SELECT Id,Category,TemplateId,ProviderBatchId,DeviceId,Status,TotalCount,SubmittedCount,DeliveredCount,FailedCount,CreatedAtUtc,CreatedByUserId,LastSyncedAtUtc FROM dbo.SmsBatches WHERE Id=@Id;", new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return r is null ? null : ToBatch(r);
    }

    public async Task<IReadOnlyList<SmsMessage>> GetMessagesByBatchIdAsync(int batchId, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<MessageRow>(new CommandDefinition(@"SELECT Id,BatchId,PersonId,StudentId,PhoneNumber,MessageBody,TemplateId,Status,ProviderMessageId,ProviderStatus,ProviderErrorCode,CreatedAtUtc,SubmittedAtUtc,SentAtUtc,DeliveredAtUtc,FailedAtUtc,LastErrorMessage,RetryCount FROM dbo.SmsMessages WHERE BatchId=@BatchId ORDER BY Id;", new { BatchId = batchId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.Select(ToMessage).ToList();
    }

    public async Task<IReadOnlyList<SmsHistoryItem>> GetHistoryAsync(DateTime? fromUtc, DateTime? toUtc, int? status, int? category, string? search, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        const string sql = @"
SELECT m.Id,m.BatchId,m.PhoneNumber,m.MessageBody,b.Category,m.Status,m.CreatedAtUtc,m.SubmittedAtUtc,m.SentAtUtc,m.DeliveredAtUtc,m.FailedAtUtc,m.LastErrorMessage,m.ProviderStatus
FROM dbo.SmsMessages m
INNER JOIN dbo.SmsBatches b ON b.Id=m.BatchId
WHERE (@FromUtc IS NULL OR m.CreatedAtUtc>=@FromUtc)
  AND (@ToUtc IS NULL OR m.CreatedAtUtc<@ToUtc)
  AND (@Status IS NULL OR m.Status=@Status)
  AND (@Category IS NULL OR b.Category=@Category)
  AND (@Search IS NULL OR m.PhoneNumber LIKE N'%' + @Search + N'%' OR m.MessageBody LIKE N'%' + @Search + N'%')
ORDER BY m.CreatedAtUtc DESC,m.Id DESC;";
        var rows = await c.QueryAsync<SmsHistoryItem>(new CommandDefinition(sql, new { FromUtc = fromUtc, ToUtc = toUtc, Status = status, Category = category, Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim() }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task UpdateBatchAsync(SmsBatch batch, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var affected = await c.ExecuteAsync(new CommandDefinition(@"UPDATE dbo.SmsBatches SET ProviderBatchId=@ProviderBatchId,Status=@Status,SubmittedCount=@SubmittedCount,DeliveredCount=@DeliveredCount,FailedCount=@FailedCount,LastSyncedAtUtc=@LastSyncedAtUtc WHERE Id=@Id;", new { batch.Id, batch.ProviderBatchId, Status = (byte)batch.Status, batch.SubmittedCount, batch.DeliveredCount, batch.FailedCount, batch.LastSyncedAtUtc }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"SmsBatch {batch.Id} was not found for update.");
    }

    public async Task UpdateMessageAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var affected = await c.ExecuteAsync(new CommandDefinition(@"UPDATE dbo.SmsMessages SET Status=@Status,ProviderMessageId=@ProviderMessageId,ProviderStatus=@ProviderStatus,ProviderErrorCode=@ProviderErrorCode,SubmittedAtUtc=@SubmittedAtUtc,SentAtUtc=@SentAtUtc,DeliveredAtUtc=@DeliveredAtUtc,FailedAtUtc=@FailedAtUtc,LastErrorMessage=@LastErrorMessage,RetryCount=@RetryCount WHERE Id=@Id;", new { message.Id, Status = (byte)message.Status, message.ProviderMessageId, message.ProviderStatus, message.ProviderErrorCode, message.SubmittedAtUtc, message.SentAtUtc, message.DeliveredAtUtc, message.FailedAtUtc, message.LastErrorMessage, message.RetryCount }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"SmsMessage {message.Id} was not found for update.");
    }

    private static SmsBatch ToBatch(BatchRow r) => SmsBatch.Load(r.Id, (SmsMessageCategory)r.Category, r.TemplateId, r.ProviderBatchId, r.DeviceId, (SmsBatchStatus)r.Status, r.TotalCount, r.SubmittedCount, r.DeliveredCount, r.FailedCount, r.CreatedAtUtc, r.CreatedByUserId, r.LastSyncedAtUtc);
    private static SmsMessage ToMessage(MessageRow r) => SmsMessage.Load(r.Id, r.BatchId, r.PersonId, r.StudentId, r.PhoneNumber, r.MessageBody, r.TemplateId, (SmsMessageStatus)r.Status, r.ProviderMessageId, r.ProviderStatus, r.ProviderErrorCode, r.CreatedAtUtc, r.SubmittedAtUtc, r.SentAtUtc, r.DeliveredAtUtc, r.FailedAtUtc, r.LastErrorMessage, r.RetryCount);
}
