using EduMaster.Application.Common;
using EduMaster.Application.Users;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.People;

public sealed class ChangePasswordViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    public ChangePasswordViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        ChangeCommand = new AsyncRelayCommand(ChangeAsync, () => !IsBusy);
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => "كلمة المرور مؤقتة — غيّرها للمتابعة";

    /// <summary>جسرا code-behind — PasswordBox لا يدعم Binding</summary>
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

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
        private set { SetProperty(ref _isBusy, value); ChangeCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand ChangeCommand { get; }
    // لا CancelCommand — التغيير إلزامي؛ الإغلاق بـ X يعيد المستخدم لشاشة الدخول

    private async Task ChangeAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword)) { ErrorMessage = "أدخل كلمة المرور الجديدة."; return; }
        if (NewPassword != ConfirmPassword) { ErrorMessage = "تأكيد كلمة المرور غير مطابق."; return; }

        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ChangePasswordHandler>();
            var result = await handler.ExecuteAsync(new ChangePasswordRequest(NewPassword, ConfirmPassword));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess("غُيّرت كلمة المرور بنجاح ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;
        }
        finally { IsBusy = false; }
    }
}