using EduMaster.Application.Abstractions;
using EduMaster.Application.Backup;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>حالة النسخ (6.5 — ن-أ): تمرير اللقطة + قرار التذكير بالسياسة (أبداً/حديثة/قديمة) · فشل المخزن ← عربي عام بلا نص خام (D-24)</summary>
public sealed class GetBackupStatusHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FileStoreFake : IBackupFileStore
    {
        public string Root { get; set; } = @"C:\Backups";
        public BackupStoreState StateToReturn { get; set; } = new(null, 0, 0);
        public Exception? ToThrow { get; set; }

        public string GetBackupRoot() => Root;
        public void SetBackupRoot(string path) => Root = path;
        public string CreateDatedBackupFolder(string root, DateTime stampLocal) => root;
        public long? ZipPhotosInto(string datedBackupFolder) => null;
        public long GetFileSize(string path) => 0;
        public void MarkBackupCompleted(string root, DateTime completedAtUtc) { }
        public BackupStoreState ReadState(string root)
        {
            if (ToThrow is not null) throw ToThrow;
            return StateToReturn;
        }
        public int CleanupOldBackups(string root, int keep) => 0;
    }

    private sealed class ClockFake : IClock
    {
        public DateTime UtcNow => FixedNow;
        public DateOnly Today => DateOnly.FromDateTime(FixedNow);
    }

    private static GetBackupStatusHandler Build(DateTime? lastBackup, int count = 0, long totalSize = 0, Exception? toThrow = null)
    {
        var files = new FileStoreFake
        {
            StateToReturn = new BackupStoreState(lastBackup, count, totalSize),
            ToThrow = toThrow,
        };
        return new GetBackupStatusHandler(files, new ClockFake(), NullLogger<GetBackupStatusHandler>.Instance);
    }

    [Fact]
    public async Task NeverBackedUp_ReminderDue()
    {
        var result = await Build(lastBackup: null).ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ReminderDue);          // لا نسخة أبداً ← تذكير (ن-4)
        Assert.Equal(0, result.Value.BackupCount);
        Assert.Equal(@"C:\Backups", result.Value.BackupRoot);
    }

    [Fact]
    public async Task RecentBackup_NoReminder()
    {
        var result = await Build(lastBackup: FixedNow.AddDays(-2), count: 3, totalSize: 10_485_760).ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.ReminderDue);         // قبل يومين — لا تذكير
        Assert.Equal(3, result.Value.BackupCount);
        Assert.Equal("10 MB", result.Value.TotalSizeText);   // الحجم المقروء بثقافة ثابتة
    }

    [Fact]
    public async Task OldBackup_ReminderDue()
    {
        var result = await Build(lastBackup: FixedNow.AddDays(-8)).ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ReminderDue);          // 8 أيام > 7 — تذكير (ن-4)
    }

    [Fact]
    public async Task StoreFailure_ArabicGeneric()
    {
        var result = await Build(lastBackup: null, toThrow: new InvalidOperationException("raw boom")).ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);   // لا نص خام (D-24)
    }
}
