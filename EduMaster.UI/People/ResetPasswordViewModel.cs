using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Users;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.People;

public sealed class ResetPasswordViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int _personId;

    public ResetPasswordViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        ResetCommand = new AsyncRelayCommand(ResetAsync, () => !IsBusy);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    private string _personName = string.Empty;
    public string PersonName { get => _personName; private set => SetProperty(ref _personName, value); }

    /// <summary>جسر من code-behind — PasswordBox لا يدعم Binding عمداً (أمان)</summary>
    public string NewPassword { get; set; } = string.Empty;

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
        private set { SetProperty(ref _isBusy, value); ResetCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand ResetCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public void Initialize(PersonListItem person)
    {
        _personId = person.Id;
        PersonName = person.FullName;
    }

    private async Task ResetAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "أدخل كلمة المرور المؤقتة الجديدة.";
            return;
        }

        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<AdminResetPasswordHandler>();
            var result = await handler.ExecuteAsync(new AdminResetPasswordRequest(_personId, NewPassword));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess("أُعيد تعيين كلمة المرور ✔ — سيُطلب تغييرها عند الدخول المقبل");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // متوقع ← بانر أحمر (D-22)
        }
        finally
        {
            IsBusy = false;
        }
    }
}