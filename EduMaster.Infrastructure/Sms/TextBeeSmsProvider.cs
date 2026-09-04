using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EduMaster.Application.Abstractions;
using EduMaster.Application.Sms;
using EduMaster.Application.Common;

namespace EduMaster.Infrastructure.Sms;

public sealed class TextBeeSmsProvider : ISmsProvider
{
    private const string BaseUrl = "https://api.textbee.dev/api/v1";
    private readonly HttpClient _http;
    private readonly ISmsSettingsStore _settings;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TextBeeSmsProvider(HttpClient http, ISmsSettingsStore settings)
    {
        _http = http;
        _http.BaseAddress ??= new Uri(BaseUrl + "/");
        _settings = settings;
    }

    public async Task<IReadOnlyList<SmsProviderDevice>> GetDevicesAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new SmsProviderException("لم يتم إدخال مفتاح TextBee API.", ErrorType.Validation);
        var doc = await SendAsync(HttpMethod.Get, "gateway/devices", null, apiKey.Trim(), cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var result = new List<SmsProviderDevice>();
        foreach (var item in data.EnumerateArray())
        {
            result.Add(new SmsProviderDevice(
                item.GetProperty("_id").GetString() ?? string.Empty,
                item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("manufacturer", out var manufacturer) ? manufacturer.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("model", out var model) ? model.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                item.TryGetProperty("isDefault", out var isDefault) && isDefault.GetBoolean(),
                ParseDate(item, "lastHeartbeat")));
        }
        return result;
    }

    public async Task<SmsProviderSendResult> SendBulkAsync(IReadOnlyList<SmsProviderMessage> messages, string deviceId, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0) throw new SmsProviderException("لا توجد رسائل للإرسال.", ErrorType.Validation);
        var payload = new
        {
            deviceId,
            messages = messages.Select(x => new { message = x.Message, recipients = new[] { x.Recipient } }).ToArray()
        };

        var doc = await SendAsync(HttpMethod.Post, "gateway/send-bulk-sms", payload, null, cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var batchId = data.TryGetProperty("smsBatchId", out var batch) ? batch.GetString() : null;
        var success = data.TryGetProperty("success", out var accepted) ? accepted.GetBoolean() : true;

        // TextBee uses recipientCount for queued batches, but returns successCount
        // for batches dispatched immediately. Prefer the actual pushed count when present.
        var acceptedCount = data.TryGetProperty("successCount", out var successCount)
            ? successCount.GetInt32()
            : data.TryGetProperty("recipientCount", out var recipientCount)
                ? recipientCount.GetInt32()
                : messages.Count;

        var failedCount = data.TryGetProperty("failureCount", out var failures)
            ? failures.GetInt32()
            : Math.Max(0, messages.Count - acceptedCount);

        return new SmsProviderSendResult(success, batchId, acceptedCount, failedCount, null);
    }

    public async Task<SmsProviderBatchStatus> GetBatchAsync(string deviceId, string providerBatchId, CancellationToken cancellationToken = default)
    {
        var path = $"gateway/devices/{Uri.EscapeDataString(deviceId)}/sms-batch/{Uri.EscapeDataString(providerBatchId)}";
        var doc = await SendAsync(HttpMethod.Get, path, null, null, cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var messages = new List<SmsProviderDeliveryMessage>();
        foreach (var item in data.GetProperty("messages").EnumerateArray())
        {
            messages.Add(new SmsProviderDeliveryMessage(
                item.TryGetProperty("_id", out var id) ? id.GetString() : null,
                item.TryGetProperty("recipient", out var recipient) ? recipient.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("status", out var status) ? status.GetString() ?? "unknown" : "unknown",
                ParseDate(item, "requestedAt"),
                ParseDate(item, "sentAt"),
                ParseDate(item, "deliveredAt"),
                ParseDate(item, "failedAt"),
                item.TryGetProperty("errorCode", out var code) ? code.GetString() : null,
                item.TryGetProperty("errorMessage", out var message) ? message.GetString() : null));
        }
        return new SmsProviderBatchStatus(providerBatchId, messages);
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string relativePath, object? body, string? apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var settings = await _settings.GetAsync(ct);
            apiKey = settings.ApiKey;
        }
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new SmsProviderException("لم يتم إعداد مفتاح TextBee API.", ErrorType.BusinessRule);

        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Add("x-api-key", apiKey);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var type = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ErrorType.BusinessRule,
                HttpStatusCode.BadRequest => ErrorType.Validation,
                HttpStatusCode.TooManyRequests => ErrorType.BusinessRule,
                HttpStatusCode.NotFound => ErrorType.NotFound,
                _ => ErrorType.Unexpected
            };
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "مفتاح TextBee غير صحيح أو لم يعد صالحًا.",
                HttpStatusCode.BadRequest => "رفضت خدمة TextBee طلب الإرسال. تحقق من الهاتف والإعدادات.",
                HttpStatusCode.TooManyRequests => "تم بلوغ حد الرسائل المتاح في خطة TextBee.",
                HttpStatusCode.NotFound => "الهاتف أو العملية المطلوبة غير موجودة في TextBee.",
                _ => "تعذّر الاتصال بخدمة TextBee."
            };
            throw new SmsProviderException(message, type);
        }
        return JsonDocument.Parse(text);
    }

    private static DateTime? ParseDate(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return DateTime.TryParse(value.GetString(), out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : null;
    }
}
