using EduMaster.UI.Common.Services;   // ⬅️ جديد: IDialogService
using EduMaster.UI.People;            // ⬅️ جديد: ChangePasswordViewModel
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

        _vm.PasswordChangeRequired += OnPasswordChangeRequired;   // ⬅️ الاشتراك الجديد
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Password = TxtPassword.Password;

    private void OnLoginSucceeded(object? sender, EventArgs e)
        => OpenMainWindow();

    /// <summary>كلمة مرور مؤقتة — ديالوغ الإلزام يُعرض قبل فتح MainWindow</summary>
    private async void OnPasswordChangeRequired(object? sender, EventArgs e)
    {
        var dialogs = _services.GetRequiredService<IDialogService>();
        var changeVm = _services.GetRequiredService<ChangePasswordViewModel>();

        var changed = await dialogs.ShowDialogAsync(changeVm, changeVm.Title);
        if (changed)
        {
            OpenMainWindow();
        }
        else
        {
            // أُغلق بلا تغيير — نبقى في شاشة الدخول (المحاولة التالية تعيد كتابة الجلسة)
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }

    private void OpenMainWindow()
    {
        var main = _services.GetRequiredService<MainWindow>();
        System.Windows.Application.Current.MainWindow = main;
        main.Show();
        Close();
    }
}