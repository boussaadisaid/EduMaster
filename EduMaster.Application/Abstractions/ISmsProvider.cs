using EduMaster.Application.Sms;

namespace EduMaster.Application.Abstractions;

public interface ISmsProvider
{
    Task<IReadOnlyList<SmsProviderDevice>> GetDevicesAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<SmsProviderSendResult> SendBulkAsync(
        IReadOnlyList<SmsProviderMessage> messages,
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<SmsProviderBatchStatus> GetBatchAsync(
        string deviceId,
        string providerBatchId,
        CancellationToken cancellationToken = default);
}
