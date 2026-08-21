using System.Windows;
using System.Windows.Controls;

namespace EduMaster.UI.Teachers;

public partial class TeacherEditorView : UserControl
{
    public TeacherEditorView()
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
            ((TeacherEditorViewModel)DataContext).SetPickedPhoto(dlg.FileName);
    }
}