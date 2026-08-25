using EduMaster.Application.Common;
using EduMaster.Application.Employees;
using EduMaster.Application.Payroll;
using EduMaster.Application.Teachers;
using EduMaster.Domain.Payroll;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Payroll;

/// <summary>
/// ديالوغ صرف أجر (5.3-هـ — D-125/س-3): وضعان —
/// · مستفيد مُعيَّن (من سطر كشف معتمد أو صف رصيد): التفكيك «مرحّل + مستحق هذا الكشف = الإجمالي» والمبلغ المقترح = البقية
/// · سلفة حرة (زر الأرصدة بلا تحديد): منتقي مستفيد — وإلا استحالت السلفة الأولى لمن لا حضور مالي له
/// معاينة حية فورية («يبقى له…» / «زيادة… تُحسب سلفة») · الملاحظة إلزامية عند التجاوز · سجل الإيصالات + تصحيح عكسي موثّق (س-5).
/// </summary>
public sealed class PayoutDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;

    private PayeeKind _payeeKind = PayeeKind.Teacher;
    private int _payeeId;
    private int? _runId;
    private long _thisRunCentimes;
    private long _balanceCentimes;

    public PayoutDialogViewModel(
        IServiceScopeFactory scopeFactory,
        IUserNotifier notifier,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _dialogs = dialogs;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CorrectCommand = new AsyncRelayCommand(CorrectAsync, () => SelectedReceipt is { IsCorrection: false });
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    public string Title => "💵 صرف أجر";
    public event EventHandler<bool>? CloseRequested;

    private string _headerText = "💵 صرف أجر";
    public string HeaderText { get => _headerText; private set => SetProperty(ref _headerText, value); }

    private string _payeeName = string.Empty;
    public string PayeeName { get => _payeeName; private set => SetProperty(ref _payeeName, value); }

    // ---------- منتقي المستفيد (وضع السلفة الحرة) ----------
    public sealed record PayeeKindOption(PayeeKind Kind, string Label);
    public sealed record PayeeOption(int Id, string Name);

    public PayeeKindOption[] PickerKindOptions { get; } = { new(PayeeKind.Teacher, "أستاذ"), new(PayeeKind.Employee, "موظف") };

    private bool _isPayeePickerVisible;
    public bool IsPayeePickerVisible { get => _isPayeePickerVisible; private set => SetProperty(ref _isPayeePickerVisible, value); }

    private PayeeKindOption? _selectedPickerKind;
    public PayeeKindOption? SelectedPickerKind
    {
        get => _selectedPickerKind;
        set
        {
            SetProperty(ref _selectedPickerKind, value);
            _ = LoadPickerPayeesAsync();
        }
    }

    public ObservableCollection<PayeeOption> PickerPayeeOptions { get; } = new();

    private PayeeOption? _selectedPickerPayee;
    public PayeeOption? SelectedPickerPayee
    {
        get => _selectedPickerPayee;
        set
        {
            SetProperty(ref _selectedPickerPayee, value);
            _ = OnPickerPayeeChangedAsync();
        }
    }

    private string _breakdownText = string.Empty;
    public string BreakdownText { get => _breakdownText; private set => SetProperty(ref _breakdownText, value); }

    // ---------- المبلغ + المعاينة الحية (UX التقريب) ----------
    private string _amountText = string.Empty;
    public string AmountText
    {
        get => _amountText;
        set
        {
            SetProperty(ref _amountText, value);
            RefreshPreview();
        }
    }

    private string _previewText = "—";
    public string PreviewText { get => _previewText; private set => SetProperty(ref _previewText, value); }

    private bool _isAdvance;
    public bool IsAdvance
    {
        get => _isAdvance;
        private set
        {
            SetProperty(ref _isAdvance, value);
            OnPropertyChanged(nameof(NoteHeaderText));
        }
    }

    public string NoteHeaderText => IsAdvance ? "الملاحظة * (إلزامية — الصرف يتجاوز الرصيد = سلفة)" : "الملاحظة (اختيارية)";

    private string _noteText = string.Empty;
    public string NoteText { get => _noteText; set => SetProperty(ref _noteText, value); }

    // ---------- سجل الإيصالات ----------
    public ObservableCollection<PayoutItem> Receipts { get; } = new();
    public bool ReceiptsEmpty => Receipts.Count == 0;

    private PayoutItem? _selectedReceipt;
    public PayoutItem? SelectedReceipt
    {
        get => _selectedReceipt;
        set
        {
            SetProperty(ref _selectedReceipt, value);
            CorrectCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CorrectCommand { get; }
    public RelayCommand CloseCommand { get; }

    // ---------- التهيئة ----------
    /// <summary>وضع المستفيد المعيَّن — من سطر كشف معتمد (runId معلوماتي) أو صف رصيد (runId فارغ).</summary>
    public async Task InitializeForPayeeAsync(PayeeKind payeeKind, int payeeId, string payeeName, int? runId, long thisRunCentimes)
    {
        IsPayeePickerVisible = false;
        _payeeKind = payeeKind;
        _payeeId = payeeId;
        _runId = runId;
        _thisRunCentimes = thisRunCentimes;
        PayeeName = payeeName;
        HeaderText = $"💵 صرف أجر — {payeeName}";

        await ReloadAsync();

        AmountText = _balanceCentimes > 0 ? MoneyInput.FormatDinars(_balanceCentimes) : string.Empty;   // المقترح = البقية
    }

    /// <summary>وضع السلفة الحرة — منتقي مستفيد (زر الأرصدة بلا تحديد: لمن لا حضور مالي له بعد).</summary>
    public async Task InitializeForFreeAdvanceAsync()
    {
        IsPayeePickerVisible = true;
        HeaderText = "💸 صرف / سلفة جديدة";
        BreakdownText = "اختر المستفيد أولاً ليظهر رصيده الجاري.";
        SelectedPickerKind = PickerKindOptions[0];   // يشغّل تحميل الخيارات
        await Task.CompletedTask;
    }

    private async Task LoadPickerPayeesAsync()
    {
        PickerPayeeOptions.Clear();
        SelectedPickerPayee = null;
        if (SelectedPickerKind is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        if (SelectedPickerKind.Kind == PayeeKind.Teacher)
        {
            var result = await scope.ServiceProvider.GetRequiredService<SearchTeachersHandler>().ExecuteAsync(null);
            if (result.IsSuccess)
                foreach (var teacher in result.Value!)
                    PickerPayeeOptions.Add(new PayeeOption(teacher.Id, teacher.FullName));
        }
        else
        {
            var result = await scope.ServiceProvider.GetRequiredService<GetEmployeesHandler>().ExecuteAsync(null);
            if (result.IsSuccess)
                foreach (var employee in result.Value!)
                    PickerPayeeOptions.Add(new PayeeOption(employee.Id, employee.FullName));
        }
    }

    // اختيار مستفيد في وضع السلفة: يجلب رصيده الجاري وسجلّه فوراً
    private async Task OnPickerPayeeChangedAsync()
    {
        var payee = SelectedPickerPayee;
        if (payee is null || SelectedPickerKind is null)
        {
            BreakdownText = "اختر المستفيد أولاً ليظهر رصيده الجاري.";
            Receipts.Clear();
            OnPropertyChanged(nameof(ReceiptsEmpty));
            AmountText = string.Empty;
            return;
        }

        _payeeKind = SelectedPickerKind.Kind;
        _payeeId = payee.Id;
        PayeeName = payee.Name;

        await ReloadAsync();
        AmountText = _balanceCentimes > 0 ? MoneyInput.FormatDinars(_balanceCentimes) : string.Empty;
    }

    private async Task ReloadAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var balancesResult = await scope.ServiceProvider.GetRequiredService<GetPayrollBalancesHandler>().ExecuteAsync();
        if (!balancesResult.IsSuccess)
        {
            _notifier.ShowError(balancesResult.ErrorMessage!);
            return;
        }

        _balanceCentimes = balancesResult.Value!
            .FirstOrDefault(b => b.PayeeKind == _payeeKind && b.PayeeId == _payeeId)?.BalanceCentimes ?? 0;

        // التفكيك: مرحّل (تلقائي — رصيد بلا هذا الكشف) + مستحق هذا الكشف = الإجمالي
        BreakdownText = _runId is not null
            ? $"مرحّل من السابق: {MoneyInput.FormatDinars(_balanceCentimes - _thisRunCentimes)} دج   +   مستحق هذا الكشف: {MoneyInput.FormatDinars(_thisRunCentimes)} دج   =   الإجمالي: {MoneyInput.FormatDinars(_balanceCentimes)} دج"
            : $"الرصيد الجاري الحالي: {MoneyInput.FormatDinars(_balanceCentimes)} دج";

        var historyResult = await scope.ServiceProvider.GetRequiredService<GetPayeePayoutsHandler>()
            .ExecuteAsync(new GetPayeePayoutsRequest(_payeeKind, _payeeId));
        if (historyResult.IsSuccess)
        {
            Receipts.Clear();
            foreach (var item in historyResult.Value!)
                Receipts.Add(item);
            OnPropertyChanged(nameof(ReceiptsEmpty));
        }
    }

    private void RefreshPreview()
    {
        if (!MoneyInput.TryParseDinars(AmountText, out var amount) || amount <= 0)
        {
            PreviewText = string.IsNullOrWhiteSpace(AmountText) ? "—" : "أدخل مبلغاً صحيحاً بالدينار (مثل 5000 أو 5252.50).";
            IsAdvance = false;
            return;
        }

        var remaining = _balanceCentimes - amount;
        IsAdvance = remaining < 0;
        PreviewText = remaining > 0
            ? $"يبقى له بعد هذا الصرف: {MoneyInput.FormatDinars(remaining)} دج"
            : remaining == 0
                ? "تصفية كاملة للرصيد ✔"
                : $"زيادة {MoneyInput.FormatDinars(-remaining)} دج تُحسب سلفة — تُستهلك من مستحقه القادم";
    }

    private async Task SaveAsync()
    {
        if (IsPayeePickerVisible && SelectedPickerPayee is null)
        {
            _notifier.ShowWarning("اختر المستفيد أولاً.");
            return;
        }
        if (!MoneyInput.TryParseDinars(AmountText, out var amount) || amount <= 0)
        {
            _notifier.ShowWarning("أدخل مبلغاً صحيحاً بالدينار أكبر من صفر.");
            return;
        }
        if (IsAdvance && string.IsNullOrWhiteSpace(NoteText))
        {
            _notifier.ShowWarning("الصرف يتجاوز الرصيد — هذه سلفة: اذكر الملاحظة لتوثيقها.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterPayoutHandler>();
        var result = await handler.ExecuteAsync(new RegisterPayoutRequest(
            _payeeKind,
            _payeeKind == PayeeKind.Teacher ? _payeeId : null,
            _payeeKind == PayeeKind.Employee ? _payeeId : null,
            _runId, amount, NoteText));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("سُجّل الصرف ✔");
            CloseRequested?.Invoke(this, true);
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    // تصحيح عكسي (س-5): قيد سالب يقابل الإيصال المحدد — الأصل يبقى موثّقاً
    private async Task CorrectAsync()
    {
        var receipt = SelectedReceipt;
        if (receipt is null || receipt.IsCorrection) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "تصحيح إيصال",
            $"سيُسجَّل قيد عكسي سالب بقيمة {MoneyInput.FormatDinars(receipt.AmountCentimes)} دج يقابل الإيصال رقم {receipt.ReceiptNo} — الإيصال الأصلي يبقى في السجل (قداسة الوثيقة).",
            "سجّل التصحيح");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterPayoutHandler>();
        var result = await handler.ExecuteAsync(new RegisterPayoutRequest(
            _payeeKind,
            _payeeKind == PayeeKind.Teacher ? _payeeId : null,
            _payeeKind == PayeeKind.Employee ? _payeeId : null,
            receipt.PayrollRunId,   // نفس مرجع الأصل — معلوماتي
            -receipt.AmountCentimes,
            $"تصحيح إيصال رقم {receipt.ReceiptNo}"));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("سُجّل قيد التصحيح ✔");
            CloseRequested?.Invoke(this, true);
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }
}