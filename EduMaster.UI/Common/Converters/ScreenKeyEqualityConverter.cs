using System;
using System.Globalization;
using System.Windows.Data;

namespace EduMaster.UI.Common.Converters;

/// <summary>F7: مساواة مفتاح الشاشة الحالية (من DataContext النافذة) بوسم بند التنقل (Tag) — يضيء البند النشط في الشريط الجانبي</summary>
public sealed class ScreenKeyEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length == 2
           && values[0] is string current
           && values[1] is string tag
           && string.Equals(current, tag, StringComparison.Ordinal);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
