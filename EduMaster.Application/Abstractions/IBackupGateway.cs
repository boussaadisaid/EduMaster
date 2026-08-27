namespace EduMaster.Application.Abstractions;

/// <summary>
/// بوابة النسخ الخام إلى SQL Server (6.5 — ن-1/ن-6): رفيعة قصداً — تنفيذ SQL الفعلي في البنية التحتية ويُجرَّب يدوياً
/// (مرآة المرسّم في 6.3)، والمنطق فوقها مختبَر بالمزيّف · لا تُستدعى وثمة معاملة قائمة (BACKUP ممنوع داخلها)
/// </summary>
public interface IBackupGateway
{
    /// <summary>BACKUP DATABASE للقاعدة المتصلة إلى ملف .bak — يرمي BackupAccessDeniedException عند رفض المجلد (SQL 3201/5: خدمة SQL Server هي الكاتبة لا التطبيق)</summary>
    Task BackupDatabaseAsync(string targetBakFilePath, CancellationToken cancellationToken = default);
}

/// <summary>رفض إذن الكتابة على مجلد النسخ — الخطأ البيئي المعروف الوحيد الذي له رسالة مرشدة خاصة (ن-1) · يحمل المسار المستهدف لتركيبها</summary>
public sealed class BackupAccessDeniedException : Exception
{
    public string FolderPath { get; }

    public BackupAccessDeniedException(string folderPath, Exception? inner = null)
        : base($"Backup target folder access denied: {folderPath}", inner) => FolderPath = folderPath;
}
