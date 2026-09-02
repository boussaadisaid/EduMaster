using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Sms;

public sealed class SmsMessage
{
    private bool _idSet;

    public int Id { get; private set; }
    public int BatchId { get; private set; }
    public int? PersonId { get; private set; }
    public int? StudentId { get; private set; }
    public string PhoneNumber { get; private set; }
    public string MessageBody { get; private set; }
    public int? TemplateId { get; private set; }
    public SmsMessageStatus Status { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? ProviderStatus { get; private set; }
    public string? ProviderErrorCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public int RetryCount { get; private set; }

    private SmsMessage(int batchId, int? personId, int? studentId, string phoneNumber, string messageBody,
        int? templateId, DateTime createdAtUtc)
    {
        Validate(batchId, phoneNumber, messageBody);
        BatchId = batchId;
        PersonId = personId;
        StudentId = studentId;
        PhoneNumber = phoneNumber.Trim();
        MessageBody = messageBody.Trim();
        TemplateId = templateId;
        Status = SmsMessageStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    private SmsMessage(int id, int batchId, int? personId, int? studentId, string phoneNumber, string messageBody,
        int? templateId, SmsMessageStatus status, string? providerMessageId, string? providerStatus,
        string? providerErrorCode, DateTime createdAtUtc, DateTime? submittedAtUtc, DateTime? sentAtUtc,
        DateTime? deliveredAtUtc, DateTime? failedAtUtc, string? lastErrorMessage, int retryCount)
        : this(batchId, personId, studentId, phoneNumber, messageBody, templateId, createdAtUtc)
    {
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        if (!Enum.IsDefined(status)) throw new DomainException("حالة الرسالة غير صالحة.");
        Id = id;
        _idSet = true;
        Status = status;
        ProviderMessageId = providerMessageId;
        ProviderStatus = providerStatus;
        ProviderErrorCode = providerErrorCode;
        SubmittedAtUtc = submittedAtUtc;
        SentAtUtc = sentAtUtc;
        DeliveredAtUtc = deliveredAtUtc;
        FailedAtUtc = failedAtUtc;
        LastErrorMessage = lastErrorMessage;
        RetryCount = retryCount;
    }

    public static SmsMessage Create(int batchId, int? personId, int? studentId, string phoneNumber, string messageBody,
        int? templateId, DateTime createdAtUtc)
        => new(batchId, personId, studentId, phoneNumber, messageBody, templateId, createdAtUtc);

    public static SmsMessage Load(int id, int batchId, int? personId, int? studentId, string phoneNumber, string messageBody,
        int? templateId, SmsMessageStatus status, string? providerMessageId, string? providerStatus,
        string? providerErrorCode, DateTime createdAtUtc, DateTime? submittedAtUtc, DateTime? sentAtUtc,
        DateTime? deliveredAtUtc, DateTime? failedAtUtc, string? lastErrorMessage, int retryCount)
        => new(id, batchId, personId, studentId, phoneNumber, messageBody, templateId, status, providerMessageId,
            providerStatus, providerErrorCode, createdAtUtc, submittedAtUtc, sentAtUtc, deliveredAtUtc,
            failedAtUtc, lastErrorMessage, retryCount);

    public void MarkSubmitted(DateTime utcNow, string? providerStatus = null)
    {
        if (Status == SmsMessageStatus.Delivered || Status == SmsMessageStatus.Cancelled) return;
        Status = SmsMessageStatus.Submitted;
        SubmittedAtUtc ??= utcNow;
        ProviderStatus = providerStatus ?? ProviderStatus;
        LastErrorMessage = null;
    }

    public void MarkSent(DateTime utcNow, string? providerMessageId, string? providerStatus)
    {
        if (Status == SmsMessageStatus.Cancelled) return;
        Status = SmsMessageStatus.Submitted;
        SubmittedAtUtc ??= utcNow;
        SentAtUtc = utcNow;
        ProviderMessageId = providerMessageId ?? ProviderMessageId;
        ProviderStatus = providerStatus ?? ProviderStatus;
        LastErrorMessage = null;
    }

    public void MarkDelivered(DateTime utcNow, string? providerMessageId, string? providerStatus)
    {
        if (Status == SmsMessageStatus.Cancelled) return;
        Status = SmsMessageStatus.Delivered;
        SubmittedAtUtc ??= utcNow;
        SentAtUtc ??= utcNow;
        DeliveredAtUtc = utcNow;
        ProviderMessageId = providerMessageId ?? ProviderMessageId;
        ProviderStatus = providerStatus ?? ProviderStatus;
        FailedAtUtc = null;
        LastErrorMessage = null;
    }

    public void MarkFailed(DateTime utcNow, string? providerStatus, string? errorCode, string? errorMessage)
    {
        if (Status == SmsMessageStatus.Delivered || Status == SmsMessageStatus.Cancelled) return;
        Status = SmsMessageStatus.Failed;
        FailedAtUtc = utcNow;
        ProviderStatus = providerStatus ?? ProviderStatus;
        ProviderErrorCode = errorCode;
        LastErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
    }

    public void IncrementRetryCount() => RetryCount++;

    private static void Validate(int batchId, string? phoneNumber, string? body)
    {
        if (batchId <= 0) throw new DomainException("معرف دفعة الرسائل غير صالح.");
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Trim().Length < 10)
            throw new DomainException("رقم الهاتف غير صالح.");
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("نص الرسالة مطلوب.");
        if (body.Trim().Length > 1000) throw new DomainException("نص الرسالة طويل جداً (الحد الأقصى 1000 حرف).");
    }

    internal void SetId(int id)
    {
        if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
