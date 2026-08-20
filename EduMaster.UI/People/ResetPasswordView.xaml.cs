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
    /// Interaction logic for ResetPasswordView.xaml
    /// </summary>
    public partial class ResetPasswordView : UserControl
    {
        public ResetPasswordView()
        {
            InitializeComponent();
        }

        private void NewPassword_Changed(object sender, RoutedEventArgs e)
        => ((ResetPasswordViewModel)DataContext).NewPassword = TxtNewPassword.Password;
    }
}
