namespace EduMaster.Application.Settings;

/// <summary>هوية المدرسة كما تُقرأ للشاشات والمطبوعات (ط-7/D-130) · LogoPath = اسم الملف المخزَّن، والمسار الكامل يُحل عبر IImageStore في الواجهة (D-38) · DisplayName يسقط على اسم المنتج عند الفراغ (D-131)</summary>
public sealed record SchoolInfoItem(int Id, string Name, string? Phone, string? Address, string? LogoPath)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "EduMaster" : Name;   // D-131
}