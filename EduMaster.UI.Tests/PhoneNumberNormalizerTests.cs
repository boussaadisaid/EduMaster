using EduMaster.UI.Common;
using Xunit;

namespace EduMaster.UI.Tests;

/// <summary>تطبيع هاتف واتساب (6.4 — ق-9): الأرقام فقط تُحفظ · 00 الدولية تُزال · الصفر المحلي ← 213 · القصير/الفارغ ← null</summary>
public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrBlank_ReturnsNull(string? phone)
        => Assert.Null(PhoneNumberNormalizer.ToWhatsAppInternational(phone));

    [Fact]
    public void LocalMobile_LeadingZeroBecomes213()
        => Assert.Equal("213550001122", PhoneNumberNormalizer.ToWhatsAppInternational("0550001122"));

    [Fact]
    public void FormattedLocal_SpacesAndDashesStripped()
        => Assert.Equal("213550001122", PhoneNumberNormalizer.ToWhatsAppInternational("0550 001-122"));

    [Fact]
    public void InternationalWithPlus_PassesThrough()
        => Assert.Equal("213550001122", PhoneNumberNormalizer.ToWhatsAppInternational("+213 550 00 11 22"));

    [Fact]
    public void InternationalWith00_PrefixNormalized()
        => Assert.Equal("213550001122", PhoneNumberNormalizer.ToWhatsAppInternational("00213550001122"));

    [Fact]
    public void TooShort_ReturnsNull()
        => Assert.Null(PhoneNumberNormalizer.ToWhatsAppInternational("1234"));

    [Fact]
    public void LandlineLocal_NormalizedBySameRule()   // الثابت 0X يُطبَّع بنفس القاعدة — صلاحية الحساب على واتساب قرار المكتب
        => Assert.Equal("21321000000", PhoneNumberNormalizer.ToWhatsAppInternational("021000000"));
}
