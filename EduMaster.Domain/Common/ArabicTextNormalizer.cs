using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EduMaster.Domain.Common;

/// <summary>
/// تطبيع النص العربي للبحث — دالة واحدة مشتركة: تُستعمل عند الحفظ (ملء FullNameNormalized)
/// وعند البحث (تطبيع المصطلح). القواعد المحسومة (ح-3):
/// أ/إ/آ/ٱ ← ا · ة ← ه · ى ← ي · إزالة التشكيل والتطويل · ضغط المسافات.
/// </summary>
public static class ArabicTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var raw = text.Trim();

        // إزالة التطويل (ـ) والتشكيل (الحركات)
        raw = raw.Replace("\u0640", "");
        raw = RemoveDiacritics(raw);

        // توحيد أشكال الألف والألف المقصورة والتاء المربوطة
        raw = raw.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ٱ', 'ا')
                 .Replace('ى', 'ي')
                 .Replace('ة', 'ه');

        // توحيد الأرقام العربية المشرقية — بحث الهاتف يعمل بأي لوحة مفاتيح
        raw = raw.Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
                 .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9');

        // ضغط المسافات المتعددة
        raw = Regex.Replace(raw, @"\s+", " ");

        return raw.ToLowerInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}