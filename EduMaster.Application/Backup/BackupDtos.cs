using System.Globalization;   // نص الحجم بثقافة ثابتة (اتساق تنسيق المال D-51)

namespace EduMaster.Application.Backup;

/// <summary>لقطة مخزن النسخ — خاماً (النصوص في الـHandler/الواجهة)</summary>
public sealed record BackupStoreState(DateTime? LastBackupAtUtc, int BackupCount, long TotalSizeBytes);

/// <summary>نتيجة نسخة ناجحة (6.5 — ن-2): المجلد المؤرخ + ملف القاعدة وحجمه + حجم حزمة الصور إن وُجدت + عدد المجلدات المنظَّفة (ن-8)</summary>
public sealed record BackupRunResult(
    string BackupFolder,
    string DatabaseFile,
    long DatabaseSizeBytes,
    long? PhotosSizeBytes,
    int CleanedUpCount,
    DateTime CompletedAtUtc);

/// <summary>حالة النسخ لتبويب الإعدادات وتذكير الإقلاع (ن-3/ن-4)</summary>
public sealed record BackupStatusItem(
    string BackupRoot,
    DateTime? LastBackupAtUtc,
    int BackupCount,
    long TotalSizeBytes,
    bool ReminderDue)
{
    /// <summary>الحجم الإجمالي مقروءاً بالميغابايت بخانة عشرية — ثقافة ثابتة</summary>
    public string TotalSizeText => (TotalSizeBytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
}
