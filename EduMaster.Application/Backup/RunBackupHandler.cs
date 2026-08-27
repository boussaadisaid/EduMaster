using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Backup;

/// <summary>
/// «نسخ الآن» (6.5 — ن-1/ن-2): جذر النسخ ← مجلد مؤرخ ← BACKUP DATABASE إلى .bak عبر البوابة (البنية) ← ضغط الصور إن وُجدت (إلحاق لا شرط) ←
/// ختم الحالة ← تنظيف بالاحتفاظ بأحدث 10 (ن-8) · لا معاملة (BACKUP ممنوع داخلها) · يرمي الإلغاء (D-64) ·
/// رفض إذن المجلد ← رسالة عربية مرشدة بالمسار (لا نص خام — D-24)
/// </summary>
public sealed class RunBackupHandler
{
    /// <summary>الاحتفاظ بأحدث 10 نسخ (ن-8)</summary>
    public const int KeepLatestCount = 10;

    private readonly IBackupGateway _gateway;
    private readonly IBackupFileStore _files;
    private readonly IClock _clock;
    private readonly ILogger<RunBackupHandler> _logger;

    public RunBackupHandler(IBackupGateway gateway, IBackupFileStore files, IClock clock, ILogger<RunBackupHandler> logger)
    {
        _gateway = gateway;
        _files = files;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OperationResult<BackupRunResult>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var root = _files.GetBackupRoot();
            var stamp = DateTime.Now;   // توقيت عمل محلي — أسماء ملفات يقرأها الإنسان
            var folder = _files.CreateDatedBackupFolder(root, stamp);

            var bakPath = Path.Combine(folder, $"EduMasterDb-{stamp:yyyyMMdd-HHmmss}.bak");
            await _gateway.BackupDatabaseAsync(bakPath, cancellationToken);
            var bakSize = _files.GetFileSize(bakPath);   // عبر المخزن — المعالج مختبَر بلا قرص حقيقي (ن-6)

            var photosSize = _files.ZipPhotosInto(folder);   // ن-2: إلحاق لا شرط

            var completedAt = _clock.UtcNow;
            _files.MarkBackupCompleted(root, completedAt);
            var cleaned = _files.CleanupOldBackups(root, KeepLatestCount);

            return OperationResult<BackupRunResult>.Success(
                new BackupRunResult(folder, bakPath, bakSize, photosSize, cleaned, completedAt));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (BackupAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Backup folder access denied: {Folder}", ex.FolderPath);
            return OperationResult<BackupRunResult>.Failure(
                $"تعذّر النسخ إلى «{ex.FolderPath}» — خدمة SQL Server لا تملك إذن الكتابة فيه. امنح الإذن لحساب الخدمة على المجلد، أو اختر مجلداً آخر من تبويب النسخ.",
                ErrorType.BusinessRule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup run failed");
            return OperationResult<BackupRunResult>.Failure("تعذّر إنشاء النسخة الاحتياطية — أعد المحاولة، فراجع مجلد النسخ.", ErrorType.Unexpected);
        }
    }
}
