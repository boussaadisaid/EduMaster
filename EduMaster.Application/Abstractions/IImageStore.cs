namespace EduMaster.Application.Abstractions;

/// <summary>تخزين صور الأشخاص — الواجهة تختار المسار فقط ولا تلمس القرص (ح-4)</summary>
public interface IImageStore
{
    /// <summary>ينسخ الصورة من مسار اختاره المستخدم إلى مخزن التطبيق، ويعيد الاسم المخزَّن (يُحفظ في PhotoPath)</summary>
    Task<string> SaveFromPathAsync(string sourcePath, CancellationToken cancellationToken = default);

    /// <summary>المسار الكامل للعرض من القيمة المخزنة — null إن لم توجد صورة</summary>
    string? GetFullPath(string? storedFileName);
}