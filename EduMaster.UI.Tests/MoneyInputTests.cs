using EduMaster.UI.Common;
using Xunit;

namespace EduMaster.UI.Tests;

/// <summary>
/// محوّل الأموال (D-51/D-67) — على تصميمه الموثق: يقبل «1500» و«1500.50» و«1500,50» والفاصلة العربية «٫»
/// · الفارغ = صفر صحيح (بلا مبلغ) · يرفض السالب وأكثر من منزلتين عشريتين · التنسيق «0.00» دائماً.
/// </summary>
public sealed class MoneyInputTests
{
    [Theory]
    [InlineData("1500", 150000)]
    [InlineData("1500,50", 150050)]   // الفاصلة الشائعة
    [InlineData("1500.50", 150050)]   // والنقطة
    [InlineData("1500٫50", 150050)]   // والفاصلة العشرية العربية
    [InlineData("1500.5", 150050)]    // منزلة واحدة مقبولة
    public void TryParseDinars_ValidInputs_ConvertToCentimes(string input, long expectedCentimes)
    {
        var ok = MoneyInput.TryParseDinars(input, out var centimes);

        Assert.True(ok);
        Assert.Equal(expectedCentimes, centimes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseDinars_EmptyOrNull_IsValidZero(string? input)   // التصميم الموثق: فارغ = صفر
    {
        var ok = MoneyInput.TryParseDinars(input, out var centimes);

        Assert.True(ok);
        Assert.Equal(0, centimes);
    }

    [Theory]
    [InlineData("abc")]        // ليس رقماً
    [InlineData("-50")]        // سالب مردود
    [InlineData("1500.555")]   // أكثر من منزلتين عشريتين
    public void TryParseDinars_InvalidInput_ReturnsFalse(string input)
    {
        Assert.False(MoneyInput.TryParseDinars(input, out _));
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(150050, "1500.50")]
    [InlineData(200000, "2000.00")]
    public void FormatDinars_AlwaysTwoDecimals(long centimes, string expected)
    {
        Assert.Equal(expected, MoneyInput.FormatDinars(centimes));
    }
}