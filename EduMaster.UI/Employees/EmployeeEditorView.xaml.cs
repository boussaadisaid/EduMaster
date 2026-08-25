using System.Windows;
using System.Windows.Controls;

namespace EduMaster.UI.Employees;

public partial class EmployeeEditorView : UserControl
{
    public EmployeeEditorView()
    {
        InitializeComponent();
    }

    private void PickPhoto_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختيار صورة",
            Filter = "الصور|*.jpg;*.jpeg;*.png"
        };

        if (dlg.ShowDialog() == true)
            ((EmployeeEditorViewModel)DataContext).SetPickedPhoto(dlg.FileName);
    }
}