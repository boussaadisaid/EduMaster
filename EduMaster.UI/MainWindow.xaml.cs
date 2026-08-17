using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.UI.AcademicYears.Services;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EduMaster.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;    
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException(
    "TEST: Unhandled exception");
        }
    }
}