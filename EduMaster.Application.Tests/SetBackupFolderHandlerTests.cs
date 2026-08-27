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

/// <summary>تعديل جذر النسخ (6.5 — ن-5): فارغ ← تحقق بلا استدعاء · نجاح يحفظ مُقلَّماً · مسار باطل/مرفوض ← عربي بالمسار (لا خام D-24) · غير متوقع عربي عام</summary>
public sealed class SetBackupFolderHandlerTests
{
    private sealed class FileStoreFake : IBackupFileStore
    {
        public string? SavedPath { get; private set; }
        public Exception? ToThrow { get; set; }

        public string GetBackupRoot() => @"C:\Backups";
        public void SetBackupRoot(string path)
        {
            if (ToThrow is not null) throw ToThrow;
            SavedPath = path;
        }
        public string CreateDatedBackupFolder(string root, DateTime stampLocal) => root;
        public long? ZipPhotosInto(string datedBackupFolder) => null;
        public long GetFileSize(string path) => 0;
        public void MarkBackupCompleted(string root, DateTime completedAtUtc) { }
        public BackupStoreState ReadState(string root) => new(null, 0, 0);
        public int CleanupOldBackups(string root, int keep) => 0;
    }

    private static (SetBackupFolderHandler handler, FileStoreFake files) Build(Exception? toThrow = null)
    {
        var files = new FileStoreFake { ToThrow = toThrow };
        return (new SetBackupFolderHandler(files, NullLogger<SetBackupFolderHandler>.Instance), files);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_Validation_StoreNotCalled(string? path)
    {
        var (handler, files) = Build();

        var result = await handler.ExecuteAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Null(files.SavedPath);   // التحقق قبل أي كتابة
    }

    [Fact]
    public async Task Success_SavesTrimmed()
    {
        var (handler, files) = Build();

        var result = await handler.ExecuteAsync("  D:\\نسخ  ");

        Assert.True(result.IsSuccess);
        Assert.Equal(@"D:\نسخ", files.SavedPath);   // يُحفظ مُقلَّماً
    }

    [Fact]
    public async Task InvalidPath_ArabicValidationWithPath()
    {
        var (handler, _) = Build(toThrow: new IOException("raw io boom"));

        var result = await handler.ExecuteAsync(@"Q:\NoSuch\Bad");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains(@"Q:\NoSuch\Bad", result.ErrorMessage!);   // المسار في الرسالة
        Assert.DoesNotContain("boom", result.ErrorMessage!);        // لا نص خام (D-24)
    }

    [Fact]
    public async Task Unexpected_ArabicGeneric()
    {
        var (handler, _) = Build(toThrow: new InvalidCastException("raw boom"));

        var result = await handler.ExecuteAsync(@"D:\Backups");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);
    }
}
