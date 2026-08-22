using System.Globalization;

namespace EduMaster.UI.Common;

/// <summary>
/// محوّل الأموال للواجهة (D-51/D-67): الدينار هنا فقط — كل ما دون الواجهة بالسنتيم (BIGINT).
/// التحليل يقبل «1500» و«1500.00» و«1500,00» والفاصلة العشرية العربية «٫» — والفارغ = صفر (بلا مبلغ).
/// </summary>
public static class MoneyInput
{
    public static bool TryParseDinars(string? text, out long centimes)
    {
        centimes = 0;
        if (string.IsNullOrWhiteSpace(text))
            return true;   // فارغ = صفر

        var normalized = text.Trim().Replace(',', '.').Replace('٫', '.');

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var dinars) || dinars < 0)
            return false;
        if (decimal.Round(dinars, 2) != dinars)
            return false;   // أكثر من منزلتين عشريتين

        centimes = (long)(dinars * 100);
        return true;
    }

    public static string FormatDinars(long centimes)
        => ((decimal)centimes / 100).ToString("0.00", CultureInfo.InvariantCulture);
}