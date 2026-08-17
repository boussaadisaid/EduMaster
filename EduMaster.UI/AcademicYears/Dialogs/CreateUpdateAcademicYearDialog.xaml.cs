using EduMaster.UI.AcademicYears.ViewModels;
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
using System.Windows.Shapes;

namespace EduMaster.UI.AcademicYears.Dialogs
{
    /// <summary>
    /// Interaction logic for CreateUpdateAcademicYearDialog.xaml
    /// </summary>
    public partial class CreateUpdateAcademicYearDialog : Window
    {
        public CreateUpdateAcademicYearDialog(CreateUpdateAcademicYearDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
