using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Backup;

/// <summary>
/// حالة النسخ لتبويب الإعدادات وتذكير الإقلاع (6.5 — ن-3/ن-4): قراءة خالصة بلا معاملة وترمي الإلغاء (D-64) ·
/// قرار «هل يُذكَّر» محسوب بالسياسة النقية BackupReminderPolicy (مختبَرة عددياً — ن-6)
/// </summary>
public sealed class GetBackupStatusHandler
{
    private readonly IBackupFileStore _files;
    private readonly IClock _clock;
    private readonly ILogger<GetBackupStatusHandler> _logger;

    public GetBackupStatusHandler(IBackupFileStore files, IClock clock, ILogger<GetBackupStatusHandler> logger)
    {
        _files = files;
        _clock = clock;
        _logger = logger;
    }

    public Task<OperationResult<BackupStatusItem>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var root = _files.GetBackupRoot();
            var state = _files.ReadState(root);
            var reminderDue = BackupReminderPolicy.ShouldRemind(state.LastBackupAtUtc, _clock.UtcNow);
            return Task.FromResult(OperationResult<BackupStatusItem>.Success(
                new BackupStatusItem(root, state.LastBackupAtUtc, state.BackupCount, state.TotalSizeBytes, reminderDue)));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read backup status");
            return Task.FromResult(OperationResult<BackupStatusItem>.Failure("تعذّرت قراءة حالة النسخ الاحتياطي.", ErrorType.Unexpected));
        }
    }
}
