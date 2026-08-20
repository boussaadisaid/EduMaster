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

namespace EduMaster.UI.People
{
    /// <summary>
    /// Interaction logic for ChangePasswordView.xaml
    /// </summary>
    public partial class ChangePasswordView : UserControl
    {
        public ChangePasswordView()
        {
            InitializeComponent();
        }

        private void New_Changed(object sender, RoutedEventArgs e)
        => ((ChangePasswordViewModel)DataContext).NewPassword = TxtNew.Password;

        private void Confirm_Changed(object sender, RoutedEventArgs e)
            => ((ChangePasswordViewModel)DataContext).ConfirmPassword = TxtConfirm.Password;
    }
}
