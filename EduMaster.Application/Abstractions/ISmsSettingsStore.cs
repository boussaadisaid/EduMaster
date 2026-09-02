using EduMaster.Application.Sms;

namespace EduMaster.Application.Abstractions;

public interface ISmsSettingsStore
{
    Task<SmsGatewaySettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SmsGatewaySettings settings, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
