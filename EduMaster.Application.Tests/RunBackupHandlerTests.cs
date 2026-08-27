using EduMaster.Application.Abstractions;
using EduMaster.Application.Backup;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>«نسخ الآن» (6.5 — ن-أ): نجاح بتركيب المسارات والختم والتنظيف باحتفاظ 10 · رفض الإذن ← عربي مرشد بالمسار (لا نص خام D-24) · صور غائبة لا تُسقط · الإلغاء يُرمى (D-64) · غير المتوقع عربي عام</summary>
public sealed class RunBackupHandlerTests
{
    /// <summary>مزيّف بوابة النسخ — يسجّل المسار ويرمي ما يُطلب</summary>
    private sealed class GatewayFake : IBackupGateway
    {
        public string? CalledWithPath { get; private set; }
        public Exception? ToThrow { get; set; }

        public Task BackupDatabaseAsync(string targetBakFilePath, CancellationToken cancellationToken = default)
        {
            CalledWithPath = targetBakFilePath;
            if (ToThrow is not null) throw ToThrow;
            return Task.CompletedTask;
        }
    }

    /// <summary>مزيّف مخزن الملفات — بلا قرص حقيقي إطلاقاً (ن-6)</summary>
    private sealed class FileStoreFake : IBackupFileStore
    {
        public string Root { get; set; } = @"C:\Backups";
        public string? CreatedFolder { get; private set; }
        public long BakSizeToReturn { get; set; } = 4096;
        public long? PhotosSizeToReturn { get; set; } = 2048;
        public DateTime? MarkedAt { get; private set; }
        public int CleanupToReturn { get; set; }
        public int CleanupKeepSeen { get; private set; } = -1;

        public string GetBackupRoot() => Root;
        public void SetBackupRoot(string path) => Root = path;
        public string CreateDatedBackupFolder(string root, DateTime stampLocal)
        {
            CreatedFolder = Path.Combine(root, stampLocal.ToString("yyyyMMdd-HHmmss"));
            return CreatedFolder;
        }
        public long? ZipPhotosInto(string datedBackupFolder) => PhotosSizeToReturn;
        public long GetFileSize(string path) => BakSizeToReturn;
        public void MarkBackupCompleted(string root, DateTime completedAtUtc) => MarkedAt = completedAtUtc;
        public BackupStoreState ReadState(string root) => new(null, 0, 0);
        public int CleanupOldBackups(string root, int keep) { CleanupKeepSeen = keep; return CleanupToReturn; }
    }

    private sealed class ClockFake : IClock
    {
        public DateTime UtcNow { get; } = new DateTime(2026, 8, 27, 16, 47, 0, DateTimeKind.Utc);
        public DateOnly Today => DateOnly.FromDateTime(UtcNow);
    }

    private static (RunBackupHandler handler, GatewayFake gateway, FileStoreFake files, ClockFake clock) Build(
        Exception? gatewayThrows = null)
    {
        var gateway = new GatewayFake { ToThrow = gatewayThrows };
        var files = new FileStoreFake();
        var clock = new ClockFake();
        return (new RunBackupHandler(gateway, files, clock, NullLogger<RunBackupHandler>.Instance), gateway, files, clock);
    }

    [Fact]
    public async Task Success_DatedFolder_BakInsideIt_StateStamped_CleanupKeepsTen()
    {
        var (handler, gateway, files, clock) = Build();

        var result = await handler.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(files.CreatedFolder);
        Assert.StartsWith(files.Root, files.CreatedFolder!);                  // المجلد المؤرخ تحت الجذر
        Assert.StartsWith(files.CreatedFolder!, gateway.CalledWithPath!);     // ملف القاعدة داخله
        Assert.EndsWith(".bak", gateway.CalledWithPath);

        var value = result.Value!;
        Assert.Equal(4096, value.DatabaseSizeBytes);
        Assert.Equal(2048, value.PhotosSizeBytes);
        Assert.Equal(clock.UtcNow, files.MarkedAt);                           // خُتمت الحالة بتوقيت الساعة المحقونة
        Assert.Equal(clock.UtcNow, value.CompletedAtUtc);
        Assert.Equal(RunBackupHandler.KeepLatestCount, files.CleanupKeepSeen);   // ن-8: الاحتفاظ بأحدث 10
    }

    [Fact]
    public async Task AccessDenied_ArabicGuidanceWithPath_NoRawText()
    {
        var (handler, _, _, _) = Build(gatewayThrows: new BackupAccessDeniedException(@"C:\Backups\20260827-174700"));

        var result = await handler.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Contains("20260827-174700", result.ErrorMessage!);   // المسار في الرسالة المرشدة (ن-1)
        Assert.Contains("SQL Server", result.ErrorMessage!);
        Assert.DoesNotContain("Exception", result.ErrorMessage!);    // لا نص خام (D-24)
    }

    [Fact]
    public async Task NoPhotos_StillSucceeds()
    {
        var (handler, _, files, _) = Build();
        files.PhotosSizeToReturn = null;

        var result = await handler.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PhotosSizeBytes);   // الصور إلحاق لا شرط (ن-2)
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64
    {
        var (handler, _, _, _) = Build(gatewayThrows: new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.ExecuteAsync());
    }

    [Fact]
    public async Task UnexpectedFailure_ArabicGeneric()
    {
        var (handler, _, _, _) = Build(gatewayThrows: new InvalidOperationException("raw boom"));

        var result = await handler.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);   // لا نص خام (D-24)
    }
}
