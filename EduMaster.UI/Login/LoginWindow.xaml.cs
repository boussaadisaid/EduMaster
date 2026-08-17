using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace EduMaster.UI;

public partial class LoginWindow : MetroWindow
{
    private readonly LoginViewModel _vm;
    private readonly IServiceProvider _services;

    public LoginWindow(LoginViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _services = services;

        Loaded += async (_, _) =>
        {
            TxtUsername.Focus();                 // المؤشر جاهز على أول حقل
            await _vm.InitializeAsync();
        };

        _vm.LoginSucceeded += OnLoginSucceeded;

        _vm.LoginFailed += (_, _) =>
        {
            TxtPassword.Clear();                 // يمسح الصندوق المرئي (لا Binding له)
            TxtPassword.Focus();
        };
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Password = TxtPassword.Password;

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        var main = _services.GetRequiredService<MainWindow>();
        System.Windows.Application.Current.MainWindow = main;
        main.Show();
        Close();
    }
}