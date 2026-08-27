using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.Application.Settings;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduMaster.UI.Billing;

/// <summary>
/// استرجاع نقدي للطالب من زائدته الدائنة فقط (D-108 — ختام UC-30): يعرض المتاح ويمنع تجاوزه · سبب إلزامي · لا يُفتح لمن لا زائدة له.
/// 6.3 (ط-هـ): بعد نجاح الصرف سؤال «طباعة إيصال الصرف الآن؟» — مرآة القبض (ط-3) بلا تخصيصات أبداً.
/// </summary>
public sealed class RefundDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogService;
    private readonly IPrintService _printService;
    private readonly ILogger<RefundDialogViewModel> _logger;

    private StudentListItem _student = null!;
    private int _studentId;
    private long _availableCreditCentimes;

    public RefundDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        IDialogService dialogService, IPrintService printService, ILogger<RefundDialogViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _dialogService = dialogService;
        _printService = printService;
        _logger = logger;

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
        _student = student;
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
                await AskAndPrintReceiptAsync(scope, result.Value, amountCentimes);   // 6.3 (ط-هـ): «طباعة الآن؟»
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

    /// <summary>
    /// 6.3 (ط-هـ): «طباعة الآن؟» بعد الصرف الناجح — مرآة القبض (ط-9): النموذج من بيانات الديالوغ المؤكَّدة،
    /// بلا «دافع» (الصرف للطالب) وبلا تخصيصات أبداً · فشل الطباعة لا يفسد نجاح الصرف — تحذير يدل على إعادة الطباعة من السجل.
    /// </summary>
    private async Task AskAndPrintReceiptAsync(AsyncServiceScope scope, int receiptNo, long amountCentimes)
    {
        if (!await _dialogService.ConfirmAsync(
                "تم الصرف ✔",
                $"إيصال صرف #{receiptNo:000000} — {MoneyInput.FormatDinars(amountCentimes)} دج\n\nهل تريد طباعة الإيصال الآن؟",
                "🖨 طباعة"))
            return;

        try
        {
            var school = await scope.ServiceProvider.GetRequiredService<GetSchoolInfoHandler>().ExecuteAsync();
            var info = school.IsSuccess ? school.Value! : new SchoolInfoItem(0, string.Empty, null, null, null);
            var header = new PrintHeader(info.DisplayName, info.Phone, info.Address, info.LogoPath);

            var model = new ReceiptPrintModel(
                header, PaymentKind.Refund, receiptNo, PaidOn!.Value,
                _student.FullName, null,   // الصرف للطالب — لا «دافع» في إيصال صرف
                amountCentimes, Reason.Trim(), new List<ReceiptAllocationPrintLine>());   // الصرف لا يُخصَّص أبداً

            if (_printService.PrintReceipt(model) == PrintOutcome.Failed)
                _notifier.ShowWarning("تعذّرت الطباعة — يمكنك إعادة طباعة الإيصال في أي وقت من سجل المدفوعات في شاشة «💰 المالية» (زر 🖨 على السطر).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print refund receipt {ReceiptNo}", receiptNo);
            _notifier.ShowWarning("تعذّرت الطباعة — يمكنك إعادة طباعة الإيصال في أي وقت من سجل المدفوعات في شاشة «💰 المالية» (زر 🖨 على السطر).");
        }
    }
}
