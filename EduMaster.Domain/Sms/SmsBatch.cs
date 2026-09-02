using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Sms;

public sealed class SmsBatch
{
    private bool _idSet;

    public int Id { get; private set; }
    public SmsMessageCategory Category { get; private set; }
    public int? TemplateId { get; private set; }
    public string? ProviderBatchId { get; private set; }
    public string? DeviceId { get; private set; }
    public SmsBatchStatus Status { get; private set; }
    public int TotalCount { get; private set; }
    public int SubmittedCount { get; private set; }
    public int DeliveredCount { get; private set; }
    public int FailedCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime? LastSyncedAtUtc { get; private set; }

    private SmsBatch(SmsMessageCategory category, int? templateId, string? deviceId, int totalCount,
        DateTime createdAtUtc, int? createdByUserId)
    {
        if (!Enum.IsDefined(category)) throw new DomainException("نوع الرسائل غير صالح.");
        if (totalCount <= 0) throw new DomainException("يجب أن يحتوي الإرسال على مستلم واحد على الأقل.");
        Category = category;
        TemplateId = templateId;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        TotalCount = totalCount;
        Status = SmsBatchStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    private SmsBatch(int id, SmsMessageCategory category, int? templateId, string? providerBatchId, string? deviceId,
        SmsBatchStatus status, int totalCount, int submittedCount, int deliveredCount, int failedCount,
        DateTime createdAtUtc, int? createdByUserId, DateTime? lastSyncedAtUtc)
        : this(category, templateId, deviceId, totalCount, createdAtUtc, createdByUserId)
    {
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        if (!Enum.IsDefined(status)) throw new DomainException("حالة الإرسال غير صالحة.");
        ValidateCounts(totalCount, submittedCount, deliveredCount, failedCount);
        Id = id;
        _idSet = true;
        ProviderBatchId = string.IsNullOrWhiteSpace(providerBatchId) ? null : providerBatchId.Trim();
        Status = status;
        SubmittedCount = submittedCount;
        DeliveredCount = deliveredCount;
        FailedCount = failedCount;
        LastSyncedAtUtc = lastSyncedAtUtc;
    }

    public static SmsBatch Create(SmsMessageCategory category, int? templateId, string? deviceId, int totalCount,
        DateTime createdAtUtc, int? createdByUserId)
        => new(category, templateId, deviceId, totalCount, createdAtUtc, createdByUserId);

    public static SmsBatch Load(int id, SmsMessageCategory category, int? templateId, string? providerBatchId, string? deviceId,
        SmsBatchStatus status, int totalCount, int submittedCount, int deliveredCount, int failedCount,
        DateTime createdAtUtc, int? createdByUserId, DateTime? lastSyncedAtUtc)
        => new(id, category, templateId, providerBatchId, deviceId, status, totalCount, submittedCount,
            deliveredCount, failedCount, createdAtUtc, createdByUserId, lastSyncedAtUtc);

    public void SetProviderBatchId(string? providerBatchId)
        => ProviderBatchId = string.IsNullOrWhiteSpace(providerBatchId) ? null : providerBatchId.Trim();

    public void Recalculate(int submittedCount, int deliveredCount, int failedCount, DateTime? syncedAtUtc)
    {
        ValidateCounts(TotalCount, submittedCount, deliveredCount, failedCount);
        SubmittedCount = submittedCount;
        DeliveredCount = deliveredCount;
        FailedCount = failedCount;
        Status = FailedCount == TotalCount
            ? SmsBatchStatus.Failed
            : DeliveredCount == TotalCount
                ? SmsBatchStatus.Completed
                : SubmittedCount > 0 || DeliveredCount > 0 || FailedCount > 0
                    ? SmsBatchStatus.Processing
                    : SmsBatchStatus.Pending;
        if (FailedCount > 0 && DeliveredCount + FailedCount >= TotalCount && DeliveredCount > 0)
            Status = SmsBatchStatus.PartialSuccess;
        LastSyncedAtUtc = syncedAtUtc;
    }

    public void MarkFailed()
    {
        FailedCount = TotalCount;
        Status = SmsBatchStatus.Failed;
    }

    private static void ValidateCounts(int total, int submitted, int delivered, int failed)
    {
        if (total <= 0 || submitted < 0 || delivered < 0 || failed < 0 ||
            submitted > total || delivered > submitted || failed > total)
            throw new DomainException("إحصاءات رسائل SMS غير صالحة.");
    }

    internal void SetId(int id)
    {
        if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
        if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");
        Id = id;
        _idSet = true;
    }
}
