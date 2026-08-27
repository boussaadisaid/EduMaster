namespace EduMaster.UI.Common;

/// <summary>
/// تطبيع هاتف جزائري لرابط واتساب wa.me (6.4 — ق-9): يُبقي الأرقام فقط · «00» الدولية تُزال ·
/// الصفر البادئ المحلي ← «213» · الناتج يلزمه 11–15 رقماً (E.164) وإلا null — الزر يتعطّل/يحذّر عندها.
/// نقي بلا تبعيات — مختبَر في UI.Tests (سابقة MoneyInput).
/// </summary>
public static class PhoneNumberNormalizer
{
    public static string? ToWhatsAppInternational(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];                                  // 00213… ← 213…
        if (digits.StartsWith("0", StringComparison.Ordinal))
            digits = "213" + digits[1..];                          // 06… ← 2136…

        return digits.Length is >= 11 and <= 15 ? digits : null;
    }
}
