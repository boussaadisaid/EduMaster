using EduMaster.Application.Backup;

namespace EduMaster.Application.Abstractions;

/// <summary>
/// مخزن ملفات النسخ الاحتياطي (6.5 — ن-2/ن-4/ن-5/ن-8): جذر قابل للتعديل بملف إعدادات محلي + مجلدات مؤرخة +
/// ضغط صور الأشخاص + ختم آخر نسخة + تنظيف بالاحتفاظ بأحدث N — ملفات لا قاعدة (روح D-04)
/// </summary>
public interface IBackupFileStore
{
    /// <summary>جذر النسخ من ملف الإعدادات المحلي أو الافتراضي</summary>
    string GetBackupRoot();

    /// <summary>يحفظ جذر النسخ (ن-5) — يُنشئ المجلد أو يقبل القائم أولاً، فالمسار الباطل/المرفوض يفشل هنا فوراً</summary>
    void SetBackupRoot(string path);

    /// <summary>ينشئ مجلد النسخة المؤرخ (yyyyMMdd-HHmmss — ترتيبه الاسمي = الزمني) داخل الجذر ويعيد مساره الكامل</summary>
    string CreateDatedBackupFolder(string root, DateTime stampLocal);

    /// <summary>يضغط مجلد صور الأشخاص (D-38) إلى Photos.zip داخل مجلد النسخة — null إن لا مجلد أو فارغ (الصور إلحاق لا شرط — روح اللوغو في 6.3)</summary>
    long? ZipPhotosInto(string datedBackupFolder);

    /// <summary>حجم ملف بالبايت — القراءة عبر المخزن لتبقى المعالجات مختبَرة بلا قرص حقيقي (ن-6)</summary>
    long GetFileSize(string path);

    /// <summary>يكتب ختم آخر نسخة ناجحة (ملف حالة في الجذر — لا جدول)</summary>
    void MarkBackupCompleted(string root, DateTime completedAtUtc);

    /// <summary>لقطة الحالة خاماً: آخر نسخة + عدد المجلدات المؤرخة + حجمها الإجمالي بالبايت</summary>
    BackupStoreState ReadState(string root);

    /// <summary>يحذف أقدم المجلدات المؤرخة فوق الاحتفاظ بأحدث keep (ن-8) — يعيد عدد المحذوف · الملف المحمي يُترك ولا يُسقط النسخة الناجحة</summary>
    int CleanupOldBackups(string root, int keep);
}
