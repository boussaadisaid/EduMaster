using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduMaster.Application.Abstractions;
using EduMaster.Application.Sms;

namespace EduMaster.Infrastructure.Sms;

/// <summary>
/// Per-Windows-user local secret store. API key never goes to SQL Server.
/// </summary>
public sealed class LocalSmsSettingsStore : ISmsSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path;
    private sealed record Persisted(string? ApiKey, string? DeviceId);

    public LocalSmsSettingsStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EduMaster", "Sms");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.dat");
    }

    public async Task<SmsGatewaySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new SmsGatewaySettings(null, null);
        var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        var item = JsonSerializer.Deserialize<Persisted>(plainBytes, JsonOptions);
        return new SmsGatewaySettings(item?.ApiKey, item?.DeviceId);
    }

    public async Task SaveAsync(SmsGatewaySettings settings, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Persisted(settings.ApiKey, settings.DeviceId), JsonOptions);
        var protectedBytes = ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser);
        var temp = _path + ".tmp";
        await File.WriteAllBytesAsync(temp, protectedBytes, cancellationToken);
        File.Move(temp, _path, true);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
