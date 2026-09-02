using System.Windows;
using System.Windows.Controls;

namespace EduMaster.UI.Sms;

public partial class SmsView : UserControl
{
    public SmsView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as SmsViewModel)?.StartPolling();
        Unloaded += (_, _) => (DataContext as SmsViewModel)?.StopPolling();
    }

    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SmsViewModel vm)
            vm.ApiKey = ApiKeyBox.Password;
    }
}
