using EduMaster.Domain.Enums;

namespace EduMaster.Application.Sms;

public sealed record SmsGatewaySettings(string? ApiKey, string? DeviceId)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(DeviceId);
}

public sealed record SmsProviderDevice(
    string Id,
    string Name,
    string Manufacturer,
    string Model,
    bool Enabled,
    bool IsDefault,
    DateTime? LastHeartbeatUtc)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"{Manufacturer} {Model}".Trim()
        : $"{Name} — {Manufacturer} {Model}".Trim();
}

public sealed record SmsProviderMessage(int LocalMessageId, string Recipient, string Message);

public sealed record SmsProviderSendResult(
    bool Accepted,
    string? ProviderBatchId,
    int AcceptedCount,
    int FailedCount,
    string? ErrorMessage);

public sealed record SmsProviderDeliveryMessage(
    string? ProviderMessageId,
    string Recipient,
    string Status,
    DateTime? RequestedAtUtc,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? FailedAtUtc,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record SmsProviderBatchStatus(
    string ProviderBatchId,
    IReadOnlyList<SmsProviderDeliveryMessage> Messages);

public sealed record SmsTemplateItem(int Id, string Name, SmsMessageCategory Category, string Body, bool IsActive)
{
    public string CategoryText => Category switch
    {
        SmsMessageCategory.DebtReminder => "تذكير بالدين",
        SmsMessageCategory.PaymentConfirmation => "تأكيد الدفع",
        SmsMessageCategory.AbsenceNotification => "إشعار الغياب",
        SmsMessageCategory.SessionBalanceNotification => "نهاية الحصص",
        SmsMessageCategory.Administrative => "رسالة إدارية",
        SmsMessageCategory.Custom => "رسالة عامة",
        _ => "—"
    };
}

public sealed record SmsHistoryItem(
    int Id,
    int BatchId,
    string PhoneNumber,
    string MessageBody,
    SmsMessageCategory Category,
    SmsMessageStatus Status,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? FailedAtUtc,
    string? LastErrorMessage,
    string? ProviderStatus)
{
    public string StatusText => Status switch
    {
        SmsMessageStatus.Pending => "قيد الانتظار",
        SmsMessageStatus.Submitted => "تم الإرسال",
        SmsMessageStatus.Delivered => "تم التسليم",
        SmsMessageStatus.Failed => "فشل",
        SmsMessageStatus.Cancelled => "ملغاة",
        _ => "—"
    };

    public string CategoryText => Category switch
    {
        SmsMessageCategory.DebtReminder => "تذكير بالدين",
        SmsMessageCategory.PaymentConfirmation => "تأكيد الدفع",
        SmsMessageCategory.AbsenceNotification => "إشعار الغياب",
        SmsMessageCategory.SessionBalanceNotification => "نهاية الحصص",
        SmsMessageCategory.Administrative => "إدارية",
        SmsMessageCategory.Custom => "عامة",
        _ => "—"
    };
}

public sealed record SmsSendRecipient(int? PersonId, int? StudentId, string PhoneNumber, string Message);
public sealed record SendSmsRequest(SmsMessageCategory Category, int? TemplateId, IReadOnlyList<SmsSendRecipient> Recipients);
public sealed record SendSmsResult(int BatchId, int RecipientCount, string? ProviderBatchId);
