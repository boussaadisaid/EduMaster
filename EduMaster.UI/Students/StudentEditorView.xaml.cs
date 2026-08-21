using System.Windows;
using System.Windows.Controls;

namespace EduMaster.UI.Students;

public partial class StudentEditorView : UserControl
{
    public StudentEditorView()
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
            ((StudentEditorViewModel)DataContext).SetPickedPhoto(dlg.FileName);
    }
}