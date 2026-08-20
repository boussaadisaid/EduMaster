using EduMaster.Application.Common;
using EduMaster.Application.Users;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.People;

public sealed class CreateAccountViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int _personId;

    public CreateAccountViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !IsBusy);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    private string _personName = string.Empty;
    public string PersonName { get => _personName; private set => SetProperty(ref _personName, value); }

    private string _username = string.Empty;
    public string Username { get => _username; set => SetProperty(ref _username, value); }

    /// <summary>جسر من code-behind — PasswordBox لا يدعم Binding عمداً (أمان)</summary>
    public string TemporaryPassword { get; set; } = string.Empty;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); 
            CreateCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public void Initialize(PersonListItem person)
    {
        _personId = person.Id;
        PersonName = person.FullName;
    }

    private async Task CreateAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Username)) { ErrorMessage = "أدخل اسم المستخدم."; return; }
        if (string.IsNullOrWhiteSpace(TemporaryPassword)) { ErrorMessage = "أدخل كلمة المرور المؤقتة."; return; }

        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateUserAccountHandler>();
            var result = await handler.ExecuteAsync(new CreateUserAccountRequest(_personId, Username, TemporaryPassword));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess("أُنشئ الحساب ✔ — سيُطلب تغيير كلمة المرور عند أول دخول");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // اسم محجوز / له حساب ← بانر (Conflict)
        }
        finally
        {
            IsBusy = false;
        }
    }
}