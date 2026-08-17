using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using EduMaster.Application.Users;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace EduMaster.UI;

public sealed class LoginViewModel : BaseViewModel
{
    private readonly IDatabaseHealthCheck _dbHealthCheck;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;
    private readonly IDatabaseInitializer _databaseInitializer;

    public LoginViewModel(
        IDatabaseHealthCheck dbHealthCheck,
        IServiceScopeFactory scopeFactory,
        CurrentUserService currentUser,
        IUserNotifier notifier,
        IDatabaseInitializer  databaseInitializer)
    {
        _dbHealthCheck = dbHealthCheck;
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
        _notifier = notifier;
        _databaseInitializer = databaseInitializer;

        RetryConnectionCommand = new AsyncRelayCommand(CheckConnectionAsync);
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => CanLogin);
    }

    // ---------- بيانات الدخول ----------

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set
        {
            SetProperty(ref _username, value);
            ValidateUsername();
            LoginCommand.RaiseCanExecuteChanged();
            if (!string.IsNullOrEmpty(value) && ErrorMessage is not null)
                ErrorMessage = null;
        }
    }

    /// <summary>جسر مؤقت من code-behind — PasswordBox لا يدعم Binding عمداً (أمان)</summary>
    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set
        {
            SetProperty(ref _password, value);
            LoginCommand.RaiseCanExecuteChanged();
            if (!string.IsNullOrEmpty(value) && ErrorMessage is not null)
                ErrorMessage = null;
        }
    }

    // TODO: «تذكرني» — ستُربط لاحقاً بتخزين جلسة آمن (DPAPI). أُخفي مربعها من الواجهة حتى تُنفَّذ فعلياً.
    private bool _rememberMe;
    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    // ---------- حالة الاتصال ----------

    private string _connectionStatusMessage = "جارٍ فحص الاتصال بقاعدة البيانات...";
    public string ConnectionStatusMessage
    {
        get => _connectionStatusMessage;
        private set => SetProperty(ref _connectionStatusMessage, value);
    }

    private bool _isCheckingConnection = true;
    public bool IsCheckingConnection
    {
        get => _isCheckingConnection;
        private set
        {
            SetProperty(ref _isCheckingConnection, value);
            OnPropertyChanged(nameof(ShowRetry));
            OnPropertyChanged(nameof(CanLogin));
            LoginCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            SetProperty(ref _isConnected, value);
            OnPropertyChanged(nameof(CanLogin));
            OnPropertyChanged(nameof(ShowRetry));
            LoginCommand.RaiseCanExecuteChanged();
        }
    }

    // ---------- حالة الدخول ----------

    private bool _isLoggingIn;
    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        private set
        {
            SetProperty(ref _isLoggingIn, value);
            OnPropertyChanged(nameof(CanLogin));
            LoginCommand.RaiseCanExecuteChanged();
        }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            SetProperty(ref _errorMessage, value);
            OnPropertyChanged(nameof(HasErrorMessage));
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanLogin => IsConnected && !IsCheckingConnection && !IsLoggingIn;

    public bool ShowRetry => !IsConnected && !IsCheckingConnection;

    // ---------- الأوامر والأحداث ----------

    public AsyncRelayCommand RetryConnectionCommand { get; }
    public AsyncRelayCommand LoginCommand { get; }

    /// <summary>تستمع له النافذة لتفتح MainWindow — الـ VM لا يعرف شيئاً عن النوافذ</summary>
    public event EventHandler? LoginSucceeded;

    /// <summary>تستمع له النافذة لتمسح صندوق كلمة المرور المرئي وتركّز عليه</summary>
    public event EventHandler? LoginFailed;

    public Task InitializeAsync() => CheckConnectionAsync();

    private async Task CheckConnectionAsync()
    {
        IsCheckingConnection = true;
        try
        {
            var result = await _dbHealthCheck.CheckAsync();
            IsConnected = result.IsSuccess;
            ConnectionStatusMessage = result.IsSuccess
                ? "متصل بقاعدة البيانات ✔"
                : result.ErrorMessage!;

            if (IsConnected)
            {
                var init = await _databaseInitializer.InitializeAsync();   // متكرر آمن — AnyUsersAsync تحرسه
                if (!init.IsSuccess)
                {
                    IsConnected = false;
                    ConnectionStatusMessage = init.ErrorMessage!;
                    return;
                }
            }
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    private async Task LoginAsync()
    {
        ErrorMessage = null;
        ValidateUsername();   // الإطار الأحمر حول الحقل عبر INotifyDataErrorInfo

        if (HasErrors || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "أدخل اسم المستخدم وكلمة المرور.";   // البانر يظهر الآن دائماً
            return;
        }

        IsLoggingIn = true;


        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();   // قاعدة Scope-per-Use-Case
            var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

            var result = await handler.ExecuteAsync(new LoginRequest(Username.Trim(), Password));

            if (result.IsSuccess)
            {
                _currentUser.SignIn(result.Value!.UserAccountId, result.Value!.Username);
                _notifier.ShowSuccess($"مرحباً بك، {result.Value!.Username}");
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Password = string.Empty;   // عادة أمنية — والنافذة تمسح الصندوق المرئي عبر LoginFailed

                if (result.ErrorType == ErrorType.Unexpected)
                {
                    _notifier.ShowError(result.ErrorMessage!);        // خطأ تقني ← Toast (D-22)
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;               // خطأ متوقع ← بانر داخلي
                    LoginFailed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    private void ValidateUsername()
    {
        if (string.IsNullOrWhiteSpace(Username))
            AddError(nameof(Username), "اسم المستخدم مطلوب");
        else
            ClearErrors(nameof(Username));
    }



}