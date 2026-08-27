using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Backup;

/// <summary>
/// تعديل جذر النسخ (6.5 — ن-5): نص غير فارغ ← المخزن يُنشئ/يقبل المجلد ويحفظه في ملف الإعدادات المحلي (بلا قاعدة) ·
/// فشل المسار المعروف (مسار باطل/مرفوض) ← تحقق إدخال عربي بالمسار (D-24)
/// </summary>
public sealed class SetBackupFolderHandler
{
    private readonly IBackupFileStore _files;
    private readonly ILogger<SetBackupFolderHandler> _logger;

    public SetBackupFolderHandler(IBackupFileStore files, ILogger<SetBackupFolderHandler> logger)
    {
        _files = files;
        _logger = logger;
    }

    public Task<OperationResult> ExecuteAsync(string? newRoot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newRoot))
            return Task.FromResult(OperationResult.Failure("حدّد مجلد النسخ أولاً.", ErrorType.Validation));

        var trimmed = newRoot.Trim();
        try
        {
            _files.SetBackupRoot(trimmed);
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Invalid backup folder: {Path}", trimmed);
            return Task.FromResult(OperationResult.Failure(
                $"تعذّر استعمال المجلد «{trimmed}» — تحقق من كتابة المسار ومن صلاحية الإذن عليه.", ErrorType.Validation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set backup folder");
            return Task.FromResult(OperationResult.Failure("تعذّر حفظ مجلد النسخ — أعد المحاولة.", ErrorType.Unexpected));
        }
    }
}
