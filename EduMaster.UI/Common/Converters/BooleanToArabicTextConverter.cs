using System.Globalization;
using System.Windows.Data;

namespace EduMaster.UI.Common.Converters;

/// <summary>يعرض حالة القيمة المنطقية للمستخدم بالعربية بدلاً من True/False.</summary>
public sealed class BooleanToArabicTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag
            ? (flag ? "فعّالة" : "معطّلة")
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
