using EduMaster.Application.Sms;
using EduMaster.Domain.Sms;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ISmsRepository
{
    Task AddBatchAsync(SmsBatch batch, CancellationToken cancellationToken = default);
    Task AddMessageAsync(SmsMessage message, CancellationToken cancellationToken = default);
    Task AddDeliveryEventAsync(SmsDeliveryEvent deliveryEvent, CancellationToken cancellationToken = default);
    Task<SmsBatch?> GetBatchByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmsMessage>> GetMessagesByBatchIdAsync(int batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmsHistoryItem>> GetHistoryAsync(DateTime? fromUtc, DateTime? toUtc, int? status, int? category,
        string? search, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(SmsBatch batch, CancellationToken cancellationToken = default);
    Task UpdateMessageAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
