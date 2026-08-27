using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EduMaster.UI.Academic
{
    /// <summary>
    /// Interaction logic for AcademicStructureView.xaml
    /// </summary>
    public partial class AcademicStructureView : UserControl
    {
        public AcademicStructureView()
        {
            InitializeComponent();
        }

        // F6 — الشريحة 6.3: منتقي لوغو المدرسة (مرآة منتقي صورة الشخص — StudentEditorView)
        private void PickLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختيار لوغو المدرسة",
                Filter = "الصور|*.jpg;*.jpeg;*.png"
            };

            if (dlg.ShowDialog() == true)
                ((AcademicStructureViewModel)DataContext).SetPickedLogo(dlg.FileName);
        }
    }
}