using System.Globalization;
using System.Windows.Data;

namespace EduMaster.UI.Common;

/// <summary>يعرض السنتيم بالدينار في الشبكات — قراءة فقط (D-51/D-67). يُسجَّل مورداً في App.xaml في الدفعة الثانية</summary>
public sealed class CentimesToDinarsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is long centimes ? MoneyInput.FormatDinars(centimes) + " دج" : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}