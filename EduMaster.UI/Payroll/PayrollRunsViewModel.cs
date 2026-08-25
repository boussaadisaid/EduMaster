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
/// شاشة «💼 الأجور» (5.2-هـ + 5.3-هـ — D-116/D-123/D-125): تبويب الكشوف (توليد/إعادة حساب/اعتماد/حذف/سطور يدوية) +
/// تبويب الأرصدة الجارية (معتمد − مصروف — الترحيل تلقائي) + ديالوغ الصرف من سطر كشف معتمد أو من صف رصيد،
/// وسلفة حرة بمنتقي مستفيد عند فتحه بلا تحديد (لمن لا حضور مالي له بعد — وإلا استحالت سلفته الأولى).
/// </summary>
public sealed class PayrollRunsViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private bool _suspendAutoDetails;   // حارس سبق التوليد/إعادة الحساب: التحميل الصريح (مع التحذيرات) هو الوحيد هناك

    public PayrollRunsViewModel(
        IServiceScopeFactory scopeFactory,
        IServiceProvider services,
        IUserNotifier notifier,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        _selectedManualKind = ManualKindOptions[0];

        RefreshCommand = new AsyncRelayCommand(() => SelectedTabIndex == 1 ? LoadBalancesAsync() : LoadRunsAsync());
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        RegenerateCommand = new AsyncRelayCommand(RegenerateAsync, () => IsSelectedDraft);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => IsSelectedDraft);
        DeleteRunCommand = new AsyncRelayCommand(DeleteRunAsync, () => IsSelectedDraft);
        AddManualLineCommand = new AsyncRelayCommand(AddManualLineAsync, () => IsSelectedDraft);
        RemoveManualLineCommand = new AsyncRelayCommand(RemoveManualLineAsync, () => IsSelectedDraft && SelectedLine is { IsManual: true });
        OpenPayoutForLineCommand = new AsyncRelayCommand(OpenPayoutForLineAsync, () => SelectedRun is { IsDraft: false } && SelectedLine is not null);
        OpenPayoutForBalanceCommand = new AsyncRelayCommand(OpenPayoutForBalanceAsync);   // يعمل دائماً: بلا تحديد = سلفة حرة بمنتقي مستفيد
    }

    // ---------- التبويبات (5.3-هـ) ----------
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            SetProperty(ref _selectedTabIndex, value);
            if (value == 1)
                _ = LoadBalancesAsync();
        }
    }

    // ---------- تبويب «💰 الأرصدة» ----------
    public ObservableCollection<PayeeBalanceItem> Balances { get; } = new();
    public bool BalancesEmpty => Balances.Count == 0;

    private PayeeBalanceItem? _selectedBalance;
    public PayeeBalanceItem? SelectedBalance
    {
        get => _selectedBalance;
        set => SetProperty(ref _selectedBalance, value);
    }

    // ---------- خيارات السطر اليدوي ----------
    public sealed record PayeeKindOption(PayeeKind Kind, string Label);
    public sealed record PayeeOption(int Id, string Name);

    public PayeeKindOption[] ManualKindOptions { get; } = { new(PayeeKind.Teacher, "أستاذ"), new(PayeeKind.Employee, "موظف") };

    private PayeeKindOption _selectedManualKind;
    public PayeeKindOption SelectedManualKind
    {
        get => _selectedManualKind;
        set
        {
            SetProperty(ref _selectedManualKind, value);
            _ = LoadPayeeOptionsAsync();
        }
    }

    public ObservableCollection<PayeeOption> PayeeOptions { get; } = new();

    private PayeeOption? _selectedPayee;
    public PayeeOption? SelectedPayee { get => _selectedPayee; set => SetProperty(ref _selectedPayee, value); }

    private string _manualAmountText = string.Empty;
    public string ManualAmountText { get => _manualAmountText; set => SetProperty(ref _manualAmountText, value); }

    private string _manualReason = string.Empty;
    public string ManualReason { get => _manualReason; set => SetProperty(ref _manualReason, value); }

    // ---------- توليد كشف جديد (افتراضي: الشهر الميلادي الحالي) ----------
    private DateTime? _newFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime? NewFrom { get => _newFrom; set => SetProperty(ref _newFrom, value); }

    private DateTime? _newTo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
    public DateTime? NewTo { get => _newTo; set => SetProperty(ref _newTo, value); }

    // ---------- القائمة ----------
    public ObservableCollection<PayrollRunListItem> Runs { get; } = new();

    private PayrollRunListItem? _selectedRun;
    public PayrollRunListItem? SelectedRun
    {
        get => _selectedRun;
        set
        {
            SetProperty(ref _selectedRun, value);
            OnPropertyChanged(nameof(IsSelectedDraft));
            OnPropertyChanged(nameof(NoRunSelected));
            OnPropertyChanged(nameof(DetailsEmpty));
            RegenerateCommand.RaiseCanExecuteChanged();
            ApproveCommand.RaiseCanExecuteChanged();
            DeleteRunCommand.RaiseCanExecuteChanged();
            AddManualLineCommand.RaiseCanExecuteChanged();
            RemoveManualLineCommand.RaiseCanExecuteChanged();
            OpenPayoutForLineCommand.RaiseCanExecuteChanged();
            if (!_suspendAutoDetails)
                _ = LoadDetailsAsync(value?.Id);
        }
    }

    public bool IsSelectedDraft => SelectedRun is { IsDraft: true };
    public bool NoRunSelected => SelectedRun is null;
    public bool DetailsEmpty => SelectedRun is not null && RunLines.Count == 0;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Runs.Count == 0;

    // ---------- التفاصيل ----------
    public ObservableCollection<PayrollLineItem> RunLines { get; } = new();

    private PayrollLineItem? _selectedLine;
    public PayrollLineItem? SelectedLine
    {
        get => _selectedLine;
        set
        {
            SetProperty(ref _selectedLine, value);
            RemoveManualLineCommand.RaiseCanExecuteChanged();
            OpenPayoutForLineCommand.RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<string> Warnings { get; } = new();
    public bool HasWarnings => Warnings.Count > 0;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand GenerateCommand { get; }
    public AsyncRelayCommand RegenerateCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand DeleteRunCommand { get; }
    public AsyncRelayCommand AddManualLineCommand { get; }
    public AsyncRelayCommand RemoveManualLineCommand { get; }
    public AsyncRelayCommand OpenPayoutForLineCommand { get; }
    public AsyncRelayCommand OpenPayoutForBalanceCommand { get; }

    public Task InitializeAsync() => LoadRunsAsync();

    // ---------- القراءات ----------
    private async Task LoadRunsAsync(int? selectId = null)
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetPayrollRunsHandler>();
            var result = await handler.ExecuteAsync();

            if (result.IsSuccess)
            {
                Runs.Clear();
                foreach (var run in result.Value!)
                    Runs.Add(run);

                var wantedId = selectId ?? SelectedRun?.Id;
                SelectedRun = wantedId is null ? null : Runs.FirstOrDefault(r => r.Id == wantedId);
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBalancesAsync()
    {
        Balances.Clear();
        SelectedBalance = null;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetPayrollBalancesHandler>().ExecuteAsync();

        if (result.IsSuccess)
            foreach (var item in result.Value!)
                Balances.Add(item);
        else
            _notifier.ShowError(result.ErrorMessage!);

        OnPropertyChanged(nameof(BalancesEmpty));
    }

    private async Task LoadDetailsAsync(int? runId, IReadOnlyList<string>? warnings = null)
    {
        RunLines.Clear();
        SelectedLine = null;
        Warnings.Clear();
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(DetailsEmpty));

        if (runId is null) return;

        var id = runId.Value;   // حارس السبق: تُقبل النتيجة فقط إن بقي نفس الكشف محدداً
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetPayrollRunDetailsHandler>();
        var result = await handler.ExecuteAsync(new GetPayrollRunDetailsRequest(id));

        if (result.IsSuccess && SelectedRun?.Id == id)
        {
            foreach (var line in result.Value!.Lines)
                RunLines.Add(line);
            if (warnings is not null)
                foreach (var warning in warnings)
                    Warnings.Add(warning);
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(DetailsEmpty));

            if (SelectedRun is { IsDraft: true })
                await LoadPayeeOptionsAsync();
        }
        else if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
        }
    }

    private async Task LoadPayeeOptionsAsync()
    {
        PayeeOptions.Clear();
        SelectedPayee = null;

        await using var scope = _scopeFactory.CreateAsyncScope();
        if (SelectedManualKind.Kind == PayeeKind.Teacher)
        {
            var result = await scope.ServiceProvider.GetRequiredService<SearchTeachersHandler>().ExecuteAsync(null);
            if (result.IsSuccess)
                foreach (var teacher in result.Value!)
                    PayeeOptions.Add(new PayeeOption(teacher.Id, teacher.FullName));
        }
        else
        {
            var result = await scope.ServiceProvider.GetRequiredService<GetEmployeesHandler>().ExecuteAsync(null);
            if (result.IsSuccess)
                foreach (var employee in result.Value!)
                    PayeeOptions.Add(new PayeeOption(employee.Id, employee.FullName));
        }
    }

    // ---------- الصرف (5.3-هـ) ----------
    private async Task OpenPayoutForLineAsync()
    {
        var run = SelectedRun;
        var line = SelectedLine;
        if (run is null || run.IsDraft || line is null) return;   // الصرف من كشف معتمد فقط — المسودة لا تصنع ديناً

        var payeeId = line.PayeeKind == PayeeKind.Teacher ? line.TeacherId : line.EmployeeId;
        if (payeeId is null) return;   // لا يحدث (قيد OnePayee قاعدةً) — دفاع

        // مستحق هذا الكشف لهذا المستفيد = Σ سطوره فيه (قد يكون سطرين: افتراضية + تجاوز)
        var thisRun = RunLines
            .Where(l => l.PayeeKind == line.PayeeKind && (l.TeacherId ?? l.EmployeeId) == payeeId.Value)
            .Sum(l => l.AmountCentimes);

        var dialog = _services.GetRequiredService<PayoutDialogViewModel>();
        await dialog.InitializeForPayeeAsync(line.PayeeKind, payeeId.Value, line.PayeeName, run.Id, thisRun);
        if (await _dialogs.ShowDialogAsync(dialog, dialog.Title))
        {
            if (SelectedTabIndex == 1) await LoadBalancesAsync();
            await LoadDetailsAsync(run.Id);
        }
    }

    // زر الأرصدة يعمل دائماً: بتحديد = صرف لصاحبه · بلا تحديد = سلفة حرة بمنتقي مستفيد
    private async Task OpenPayoutForBalanceAsync()
    {
        var balance = SelectedBalance;
        var dialog = _services.GetRequiredService<PayoutDialogViewModel>();

        if (balance is not null)
            await dialog.InitializeForPayeeAsync(balance.PayeeKind, balance.PayeeId, balance.PayeeName, null, 0);
        else
            await dialog.InitializeForFreeAdvanceAsync();

        if (await _dialogs.ShowDialogAsync(dialog, dialog.Title))
            await LoadBalancesAsync();
    }

    // ---------- الأفعال على الكشوف ----------
    private async Task GenerateAsync()
    {
        if (NewFrom is null || NewTo is null)
        {
            _notifier.ShowWarning("حدّد طرفي الفترة (من / إلى).");
            return;
        }
        if (NewTo.Value < NewFrom.Value)
        {
            _notifier.ShowWarning("نهاية الفترة لا يمكن أن تسبق بدايتها.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GeneratePayrollRunHandler>();
        var result = await handler.ExecuteAsync(new GeneratePayrollRunRequest(
            DateOnly.FromDateTime(NewFrom.Value), DateOnly.FromDateTime(NewTo.Value)));

        if (!result.IsSuccess)
        {
            if (result.ErrorType == ErrorType.Unexpected) _notifier.ShowError(result.ErrorMessage!);
            else _notifier.ShowWarning(result.ErrorMessage!);   // التقاطع مع معتمدة أو تكديس مسودة — تحذيري (D-29)
            return;
        }

        var gen = result.Value!;
        _notifier.ShowSuccess($"وُلّدت المسودة ✔ — {gen.LinesCount} سطراً");

        _suspendAutoDetails = true;
        try { await LoadRunsAsync(gen.RunId); }
        finally { _suspendAutoDetails = false; }
        await LoadDetailsAsync(gen.RunId, gen.Warnings);
    }

    private async Task RegenerateAsync()
    {
        var run = SelectedRun;
        if (run is not { IsDraft: true }) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "إعادة حساب المسودة",
            $"ستُسقط السطور المحسوبة لكشف «{run.PeriodText}» وتُعاد من المصدر الحيّ (الحصص والأيام والسياسات الآن) — السطور اليدوية تبقى كما هي.",
            "أعد الحساب");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegeneratePayrollRunHandler>();
        var result = await handler.ExecuteAsync(new RegeneratePayrollRunRequest(run.Id));

        if (result.IsSuccess)
        {
            var gen = result.Value!;
            _notifier.ShowSuccess($"أُعيد الحساب ✔ — {gen.LinesCount} سطراً");
            _suspendAutoDetails = true;
            try { await LoadRunsAsync(run.Id); }
            finally { _suspendAutoDetails = false; }
            await LoadDetailsAsync(run.Id, gen.Warnings);
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task ApproveAsync()
    {
        var run = SelectedRun;
        if (run is not { IsDraft: true }) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "اعتماد الكشف — نقطة اللاعودة",
            $"الاعتماد يقفل الكشف نهائياً: لا تعديل ولا حذف ولا إعادة حساب بعده (الخطأ يُصحَّح بصرف تسوية).\n\nالفترة: {run.PeriodText}\nالإجمالي: {MoneyInput.FormatDinars(run.TotalCentimes)} دج\nعدد السطور: {run.LinesCount}\n\nاعتمد؟",
            "اعتمد نهائياً");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ApprovePayrollRunHandler>();
        var result = await handler.ExecuteAsync(new ApprovePayrollRunRequest(run.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("اعتُمد الكشف ✔ — قُفل نهائياً");
            await LoadRunsAsync(run.Id);
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task DeleteRunAsync()
    {
        var run = SelectedRun;
        if (run is not { IsDraft: true }) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "حذف المسودة",
            $"ستُحذف مسودة «{run.PeriodText}» وسطورها ({run.LinesCount}) نهائياً — لا تراجع.",
            "احذف المسودة");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeletePayrollRunHandler>();
        var result = await handler.ExecuteAsync(new DeletePayrollRunRequest(run.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("حُذفت المسودة ✔");
            await LoadRunsAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    // ---------- السطور اليدوية (س-8) ----------
    private async Task AddManualLineAsync()
    {
        var run = SelectedRun;
        if (run is not { IsDraft: true }) return;

        if (SelectedPayee is null)
        {
            _notifier.ShowWarning("اختر المستفيد (أستاذ / موظف).");
            return;
        }

        // المبلغ يقبل السالب (خصم) — MoneyInput ترفض السالب فنحلّل القيمة المطلقة ونطبّق الإشارة
        var rawAmount = (ManualAmountText ?? string.Empty).Trim();
        var isDeduction = rawAmount.StartsWith('-');
        var digits = isDeduction ? rawAmount[1..].Trim() : rawAmount;
        if (!MoneyInput.TryParseDinars(digits, out var absoluteAmount) || absoluteAmount <= 0)
        {
            _notifier.ShowWarning("أدخل مبلغاً صحيحاً بالدينار — موجباً للمكافأة (مثل 5000) أو سالباً للخصم (مثل -2000).");
            return;
        }
        if (string.IsNullOrWhiteSpace(ManualReason))
        {
            _notifier.ShowWarning("اذكر سبب السطر (مكافأة / خصم) — إلزامي للتوثيق.");
            return;
        }

        var amount = isDeduction ? -absoluteAmount : absoluteAmount;
        var request = new AddManualPayrollLineRequest(run.Id, SelectedManualKind.Kind,
            SelectedManualKind.Kind == PayeeKind.Teacher ? SelectedPayee.Id : null,
            SelectedManualKind.Kind == PayeeKind.Employee ? SelectedPayee.Id : null,
            amount, ManualReason.Trim());

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<AddManualPayrollLineHandler>();
        var result = await handler.ExecuteAsync(request);

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("أُضيف السطر اليدوي ✔");
            ManualAmountText = string.Empty;
            ManualReason = string.Empty;
            _suspendAutoDetails = true;
            try { await LoadRunsAsync(run.Id); }
            finally { _suspendAutoDetails = false; }
            await LoadDetailsAsync(run.Id);
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task RemoveManualLineAsync()
    {
        var run = SelectedRun;
        var line = SelectedLine;
        if (run is not { IsDraft: true } || line is not { IsManual: true }) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "حذف سطر يدوي",
            $"سيُحذف السطر اليدوي لـ«{line.PayeeName}» ({MoneyInput.FormatDinars(line.AmountCentimes)} دج — {line.Details}).",
            "احذف السطر");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RemoveManualPayrollLineHandler>();
        var result = await handler.ExecuteAsync(new RemoveManualPayrollLineRequest(run.Id, line.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("حُذف السطر ✔");
            _suspendAutoDetails = true;
            try { await LoadRunsAsync(run.Id); }
            finally { _suspendAutoDetails = false; }
            await LoadDetailsAsync(run.Id);
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }
}