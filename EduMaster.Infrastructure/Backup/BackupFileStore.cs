using EduMaster.Application.Abstractions;
using EduMaster.Application.Backup;
using System.IO.Compression;
using System.Text.Json;

namespace EduMaster.Infrastructure.Backup;

/// <summary>
/// مخزن ملفات النسخ (6.5 — ن-2/ن-4/ن-5/ن-8): الجذر من LocalApplicationData\EduMaster\settings.json (الافتراضي C:\EduMasterBackups) ·
/// نسخة = مجلد مؤرخ yyyyMMdd-HHmmss فيه EduMasterDb-….bak + Photos.zip إن وُجدت صور · الحالة last-backup.json في الجذر · التنظيف يحتفظ بأحدث N ·
/// مجلد الصور مرآة مسار ImageStore (D-38) — يُقرأ منه فقط ولا يُكتب إليه من هنا أبداً
/// </summary>
public sealed class BackupFileStore : IBackupFileStore
{
    private const string DefaultRoot = @"C:\EduMasterBackups";
    private const string SettingsFileName = "settings.json";
    private const string StateFileName = "last-backup.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFolder;
    private readonly string _photosFolder;

    public BackupFileStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsFolder = Path.Combine(localAppData, "EduMaster");
        Directory.CreateDirectory(_settingsFolder);
        _photosFolder = Path.Combine(localAppData, "SchoolSys", "Photos");   // مرآة مسار ImageStore (D-38) — قراءة فقط
    }

    public string GetBackupRoot()
    {
        var settings = ReadSettings();
        return string.IsNullOrWhiteSpace(settings.BackupRoot) ? DefaultRoot : settings.BackupRoot!;
    }

    public void SetBackupRoot(string path)
    {
        Directory.CreateDirectory(path);   // يُنشئ أو يقبل القائم — ويكشف المسار الباطل/المرفوض فوراً (ن-5)
        WriteSettings(new LocalSettings(path));
    }

    public string CreateDatedBackupFolder(string root, DateTime stampLocal)
    {
        var folder = Path.Combine(root, stampLocal.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public long? ZipPhotosInto(string datedBackupFolder)
    {
        if (!Directory.Exists(_photosFolder) || !Directory.EnumerateFiles(_photosFolder).Any())
            return null;   // لا صور — إلحاق لا شرط (ن-2)

        var zipPath = Path.Combine(datedBackupFolder, "Photos.zip");
        ZipFile.CreateFromDirectory(_photosFolder, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);
        return new FileInfo(zipPath).Length;
    }

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public void MarkBackupCompleted(string root, DateTime completedAtUtc)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, StateFileName),
            JsonSerializer.Serialize(new BackupStateFile(completedAtUtc), JsonOptions));
    }

    public BackupStoreState ReadState(string root)
    {
        DateTime? last = null;
        var statePath = Path.Combine(root, StateFileName);
        if (File.Exists(statePath))
        {
            try
            {
                last = JsonSerializer.Deserialize<BackupStateFile>(File.ReadAllText(statePath))?.LastBackupAtUtc;
            }
            catch
            {
                last = null;   // ملف حالة تالف = كأن لا نسخة — يظهر التذكير (روح D-124: لا اختفاء صامت)
            }
        }

        if (!Directory.Exists(root))
            return new BackupStoreState(last, 0, 0);

        var folderCount = Directory.GetDirectories(root).Length;
        long totalSize = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            totalSize += new FileInfo(file).Length;

        return new BackupStoreState(last, folderCount, totalSize);
    }

    public int CleanupOldBackups(string root, int keep)
    {
        if (!Directory.Exists(root))
            return 0;

        var datedFolders = Directory.GetDirectories(root)
            .Select(path => new DirectoryInfo(path))
            .Where(dir => IsDatedFolderName(dir.Name))
            .OrderByDescending(dir => dir.Name)   // yyyyMMdd-HHmmss: الترتيب الاسمي = الزمني
            .ToList();

        var deleted = 0;
        foreach (var old in datedFolders.Skip(Math.Max(keep, 0)))
        {
            try
            {
                old.Delete(recursive: true);
                deleted++;
            }
            catch
            {
                // ملف مفتوح أو محمي — يُترك ولا يُسقط النسخة الناجحة (النظافة ثانوية أمام النسخ)
            }
        }
        return deleted;
    }

    private static bool IsDatedFolderName(string name)
        => name.Length == 15 && name[8] == '-' && name.All(c => char.IsDigit(c) || c == '-');

    // ═══ ملف الإعدادات المحلي (ن-5) — JSON صغير بلا قاعدة (روح D-04) ═══
    private string SettingsPath => Path.Combine(_settingsFolder, SettingsFileName);

    private LocalSettings ReadSettings()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(SettingsPath)) ?? new LocalSettings(null)
                : new LocalSettings(null);
        }
        catch
        {
            return new LocalSettings(null);   // إعدادات تالفة ← الافتراضي — لا تُسقط القراءة أبداً
        }
    }

    private void WriteSettings(LocalSettings settings)
        => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));

    private sealed record LocalSettings(string? BackupRoot);
    private sealed record BackupStateFile(DateTime LastBackupAtUtc);
}
