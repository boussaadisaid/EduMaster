using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Sms;

public sealed class SmsDeliveryEvent
{
    private bool _idSet;
    public int Id { get; private set; }
    public int SmsMessageId { get; private set; }
    public SmsMessageStatus Status { get; private set; }
    public string? ProviderStatus { get; private set; }
    public string? ProviderErrorCode { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? RawPayload { get; private set; }

    private SmsDeliveryEvent(int smsMessageId, SmsMessageStatus status, string? providerStatus,
        string? providerErrorCode, DateTime occurredAtUtc, string? rawPayload)
    {
        if (smsMessageId <= 0) throw new DomainException("معرف الرسالة غير صالح.");
        if (!Enum.IsDefined(status)) throw new DomainException("حالة الرسالة غير صالحة.");
        SmsMessageId = smsMessageId;
        Status = status;
        ProviderStatus = providerStatus;
        ProviderErrorCode = providerErrorCode;
        OccurredAtUtc = occurredAtUtc;
        RawPayload = rawPayload;
    }

    public static SmsDeliveryEvent Create(int smsMessageId, SmsMessageStatus status, string? providerStatus,
        string? providerErrorCode, DateTime occurredAtUtc, string? rawPayload)
        => new(smsMessageId, status, providerStatus, providerErrorCode, occurredAtUtc, rawPayload);

    public static SmsDeliveryEvent Load(int id, int smsMessageId, SmsMessageStatus status, string? providerStatus,
        string? providerErrorCode, DateTime occurredAtUtc, string? rawPayload)
    {
        var item = new SmsDeliveryEvent(smsMessageId, status, providerStatus, providerErrorCode, occurredAtUtc, rawPayload);
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        item.Id = id;
        item._idSet = true;
        return item;
    }

    internal void SetId(int id)
    {
        if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
