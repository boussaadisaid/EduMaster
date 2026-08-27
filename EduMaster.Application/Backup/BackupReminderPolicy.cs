namespace EduMaster.Application.Backup;

/// <summary>سياسة تذكير النسخ (6.5 — ن-4): لا نسخة أبداً أو مضى عليها أكثر من 7 أيام ← تذكير · نقية ثابتة مختبَرة عددياً (ن-6)</summary>
public static class BackupReminderPolicy
{
    public const int RemindAfterDays = 7;

    public static bool ShouldRemind(DateTime? lastBackupAtUtc, DateTime utcNow)
        => lastBackupAtUtc is null || (utcNow - lastBackupAtUtc.Value).TotalDays > RemindAfterDays;
}
