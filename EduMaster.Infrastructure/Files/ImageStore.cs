using EduMaster.Application.Abstractions;

namespace EduMaster.Infrastructure.Files;

public sealed class ImageStore : IImageStore
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    private const long MaxBytes = 5L * 1024 * 1024;   // 5MB

    private readonly string _folder;

    public ImageStore()
    {
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SchoolSys", "Photos");
        Directory.CreateDirectory(_folder);
    }

    public async Task<string> SaveFromPathAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Unsupported image extension: {extension}");

        var info = new FileInfo(sourcePath);
        if (!info.Exists)
            throw new InvalidOperationException($"Image file not found: {sourcePath}");
        if (info.Length > MaxBytes)
            throw new InvalidOperationException("Image exceeds the 5MB limit.");

        // نسخ إلى مخزن التطبيق باسم GUID — لا إشارة لمسار المستخدم الأصلي أبداً
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var targetPath = Path.Combine(_folder, storedFileName);

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);

        return storedFileName;
    }

    public string? GetFullPath(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            return null;

        var fullPath = Path.Combine(_folder, storedFileName);
        return File.Exists(fullPath) ? fullPath : null;
    }
}