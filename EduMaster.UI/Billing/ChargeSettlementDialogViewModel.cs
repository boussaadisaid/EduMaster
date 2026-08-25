using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Billing;

/// <summary>تسوية مستحق موثقة (D-108): إلغاء أو تخفيض — كلاهما بسبب إلزامي، وبلا حذف إطلاقاً (D-109)</summary>
public sealed class ChargeSettlementDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int _chargeId;
    private bool _isReduction;
    private long _currentAmountCentimes;

    public ChargeSettlementDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _isReduction ? "تخفيض مستحق" : "إلغاء مستحق";

    private string _contextText = string.Empty;
    public string ContextText
    {
        get => _contextText;
        private set => SetProperty(ref _contextText, value);
    }

    // ---------- التخفيض فقط ----------
    public bool ShowAmountField => _isReduction;

    public string CurrentAmountText => $"{MoneyInput.FormatDinars(_currentAmountCentimes)} دج";

    private string _newAmountText = string.Empty;
    public string NewAmountText
    {
        get => _newAmountText;
        set => SetProperty(ref _newAmountText, value);
    }

    // ---------- المشترك ----------
    private string _reason = string.Empty;
    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set { SetProperty(ref _isSaving, value); SaveCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public void Initialize(StudentChargeItem charge, string studentName, bool isReduction)
    {
        _chargeId = charge.Id;
        _isReduction = isReduction;
        _currentAmountCentimes = charge.AmountCentimes;

        ContextText = $"{studentName} — {charge.KindText} · {charge.SourceDescription}";
        NewAmountText = string.Empty;   // فارغ = إعفاء كامل 0 (تصميم المحوّل الموثق)
        Reason = string.Empty;
        ErrorMessage = null;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ShowAmountField));
        OnPropertyChanged(nameof(CurrentAmountText));
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "سبب التسوية إلزامي — السجل المالي موثَّق دائماً (D-108).";
            return;
        }

        long newAmountCentimes = 0;
        if (_isReduction)
        {
            if (!MoneyInput.TryParseDinars(NewAmountText, out newAmountCentimes))
            {
                ErrorMessage = "أدخل مبلغاً صحيحاً بالدينار — والفارغ = إعفاء كامل (0).";
                return;
            }
            if (newAmountCentimes >= _currentAmountCentimes)
            {
                ErrorMessage = $"التخفيض يقتضي مبلغاً أقل من الحالي ({CurrentAmountText}).";
                return;
            }
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            OperationResult result;
            if (_isReduction)
            {
                var handler = scope.ServiceProvider.GetRequiredService<ReduceChargeHandler>();
                result = await handler.ExecuteAsync(new ReduceChargeRequest(_chargeId, newAmountCentimes, Reason));
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<CancelChargeHandler>();
                result = await handler.ExecuteAsync(new CancelChargeRequest(_chargeId, Reason));
            }

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess(_isReduction
                    ? $"خُفّض المستحق إلى {MoneyInput.FormatDinars(newAmountCentimes)} دج ✔"
                    : "أُلغي المستحق ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // قواعد متوقعة (مسوّى مسبقاً…) ← البانر (D-22)
        }
        finally
        {
            IsSaving = false;
        }
    }
}