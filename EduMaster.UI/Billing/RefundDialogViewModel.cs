using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Students;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Billing;

/// <summary>استرجاع نقدي للطالب من زائدته الدائنة فقط (D-108 — ختام UC-30): يعرض المتاح ويمنع تجاوزه · سبب إلزامي · لا يُفتح لمن لا زائدة له</summary>
public sealed class RefundDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int _studentId;
    private long _availableCreditCentimes;

    public RefundDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    public string Title => "استرجاع نقدي (إيصال صرف)";

    private string _contextText = string.Empty;
    public string ContextText
    {
        get => _contextText;
        private set => SetProperty(ref _contextText, value);
    }

    private string _creditText = string.Empty;
    public string CreditText
    {
        get => _creditText;
        private set => SetProperty(ref _creditText, value);
    }

    private string _amountText = string.Empty;
    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    private DateTime? _paidOn = DateTime.Today;
    public DateTime? PaidOn
    {
        get => _paidOn;
        set => SetProperty(ref _paidOn, value);
    }

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

    public async Task InitializeAsync(StudentListItem student)
    {
        _studentId = student.Id;
        ContextText = $"الطالب: {student.FullName}";

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetPaymentContextHandler>();
        var result = await handler.ExecuteAsync(student.Id);

        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            CloseRequested?.Invoke(this, false);
            return;
        }

        _availableCreditCentimes = result.Value!.UnallocatedCentimes;
        if (_availableCreditCentimes <= 0)
        {
            // لا زائدة ⇒ لا شيء يُسترجع — لا يُفتح الديالوغ أصلاً (الحارس الخلفي موجود أيضاً)
            _notifier.ShowWarning("لا زائدة دائنة لهذا الطالب — لا شيء يُسترجع.");
            CloseRequested?.Invoke(this, false);
            return;
        }

        CreditText = $"الزائدة الدائنة المتاحة: {MoneyInput.FormatDinars(_availableCreditCentimes)} دج";
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!MoneyInput.TryParseDinars(AmountText, out var amountCentimes) || amountCentimes <= 0)
        {
            ErrorMessage = "أدخل مبلغ الاسترجاع بالدينار — أكبر من صفر.";
            return;
        }
        if (amountCentimes > _availableCreditCentimes)
        {
            ErrorMessage = $"المبلغ يتجاوز الزائدة الدائنة المتاحة ({MoneyInput.FormatDinars(_availableCreditCentimes)} دج) — لا صرف من الهواء.";
            return;
        }
        if (PaidOn is null)
        {
            ErrorMessage = "اختر تاريخ الصرف.";
            return;
        }
        if (PaidOn.Value.Date > DateTime.Today)
        {
            ErrorMessage = "تاريخ الصرف لا يمكن أن يكون في المستقبل.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "سبب الاسترجاع إلزامي — المال الخارج يُوثَّق دائماً.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<RegisterRefundHandler>();
            var result = await handler.ExecuteAsync(new RegisterRefundRequest(
                _studentId, amountCentimes, DateOnly.FromDateTime(PaidOn.Value), Reason));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"صُرف {MoneyInput.FormatDinars(amountCentimes)} دج استرجاعاً — إيصال #{result.Value:000000} ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // حُراس الصرف ← البانر (D-22)
        }
        finally
        {
            IsSaving = false;
        }
    }
}