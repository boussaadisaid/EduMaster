using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.Application.Settings;
using EduMaster.Application.Treasury;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Billing;

/// <summary>
/// ديالوغ القبض (D-104…D-107): مبلغ + تاريخ (اليوم افتراضاً) + «الولي المسجَّل هو الدافع» عند وجوده
/// + تخصيص مقترح تلقائياً (الأقدم أولاً) قابل للتعديل + الزائدة الدائنة مرئية حيّة.
/// القبض متاح دائماً — الدين لا يموت بتعطيل الطالب.
/// 6.3 (ط-هـ): بعد نجاح القبض سؤال «طباعة الإيصال الآن؟» — النموذج يُركَّب من بيانات الديالوغ المؤكدة نفسها (ط-9).
/// </summary>
public sealed class PaymentDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogService;
    private readonly IPrintService _printService;
    private readonly ILogger<PaymentDialogViewModel> _logger;

    private StudentListItem _student = null!;
    private int _studentId;
    private int? _guardianPersonId;
    private IReadOnlyList<OpenChargeItem> _currentYearOpenCharges = Array.Empty<OpenChargeItem>();
    private IReadOnlyList<OpenChargeItem> _otherYearsOpenCharges = Array.Empty<OpenChargeItem>();
    private IReadOnlyList<OpenChargeItem> _openCharges = Array.Empty<OpenChargeItem>();
    private string _academicYearText = string.Empty;
    private bool _showOtherYears;

    public PaymentDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        IDialogService dialogService, IPrintService printService, ILogger<PaymentDialogViewModel> logger)
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

    public string Title => "قبض دفعة";

    private string _contextText = string.Empty;
    public string ContextText
    {
        get => _contextText;
        private set => SetProperty(ref _contextText, value);
    }

    // ---------- المبلغ والتاريخ والدافع ----------
    private string _amountText = string.Empty;
    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
                ApplySuggestion();   // D-106: الاقتراح يتجدد مع المبلغ
        }
    }

    private DateTime? _paidOn = DateTime.Today;
    public DateTime? PaidOn
    {
        get => _paidOn;
        set => SetProperty(ref _paidOn, value);
    }

    public bool ShowGuardianOption { get; private set; }

    private string _guardianOptionText = string.Empty;
    public string GuardianOptionText
    {
        get => _guardianOptionText;
        private set => SetProperty(ref _guardianOptionText, value);
    }

    private bool _paidByGuardian;
    public bool PaidByGuardian
    {
        get => _paidByGuardian;
        set => SetProperty(ref _paidByGuardian, value);
    }

    private string _note = string.Empty;
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    // ---------- التخصيص ----------
    public ObservableCollection<PaymentAllocationRowViewModel> Rows { get; } = new();

    public ObservableCollection<TreasuryAccountItem> TreasuryAccounts { get; } = new();
    private TreasuryAccountItem? _selectedTreasuryAccount;
    public TreasuryAccountItem? SelectedTreasuryAccount { get => _selectedTreasuryAccount; set => SetProperty(ref _selectedTreasuryAccount, value); }

    public bool HasOpenCharges => Rows.Count > 0;

    public bool HasAnyCharges => _currentYearOpenCharges.Count > 0 || _otherYearsOpenCharges.Count > 0;

    public bool NoOpenCharges => Rows.Count == 0 && !HasOtherYears;

    public bool NoCurrentYearCharges => _currentYearOpenCharges.Count == 0;

    public string AcademicYearText
    {
        get => _academicYearText;
        private set => SetProperty(ref _academicYearText, value);
    }

    public bool HasOtherYears => _otherYearsOpenCharges.Count > 0;

    public bool ShowOtherYears
    {
        get => _showOtherYears;
        set
        {
            if (SetProperty(ref _showOtherYears, value))
            {
                RebuildVisibleCharges();
                OnPropertyChanged(nameof(OtherYearsButtonText));
            }
        }
    }

    public string OtherYearsButtonText => _showOtherYears
        ? "إخفاء مستحقات السنوات الأخرى"
        : $"عرض المستحقات في سنوات أخرى ({_otherYearsOpenCharges.Count})";

    private void RebuildVisibleCharges()
    {
        _openCharges = _showOtherYears
            ? _currentYearOpenCharges.Concat(_otherYearsOpenCharges).ToList()
            : _currentYearOpenCharges;

        Rows.Clear();
        foreach (var charge in _openCharges)
        {
            var sourceText = string.IsNullOrWhiteSpace(charge.AcademicYearName)
                ? charge.SourceDescription
                : $"{charge.SourceDescription} — {charge.AcademicYearName}";

            Rows.Add(new PaymentAllocationRowViewModel(
                charge.Id, charge.KindText, sourceText,
                $"{MoneyInput.FormatDinars(charge.RemainingCentimes)} دج", RecomputeUnallocated));
        }

        OnPropertyChanged(nameof(HasOpenCharges));
        OnPropertyChanged(nameof(HasAnyCharges));
        OnPropertyChanged(nameof(NoOpenCharges));
        OnPropertyChanged(nameof(NoCurrentYearCharges));
        ApplySuggestion();
    }


    private string _creditText = string.Empty;
    public string CreditText   // D-107: الزائدة الدائنة المتاحة من قبل
    {
        get => _creditText;
        private set { SetProperty(ref _creditText, value); OnPropertyChanged(nameof(HasCredit)); }
    }

    public bool HasCredit => !string.IsNullOrEmpty(CreditText);

    private string _unallocatedFromThisText = string.Empty;
    public string UnallocatedFromThisText   // حيّ: غير مخصص من هذه الدفعة
    {
        get => _unallocatedFromThisText;
        private set { SetProperty(ref _unallocatedFromThisText, value); OnPropertyChanged(nameof(HasUnallocatedFromThis)); }
    }

    public bool HasUnallocatedFromThis => !string.IsNullOrEmpty(UnallocatedFromThisText);

    // ---------- الخطأ والانشغال ----------
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

    // ---------- التهيئة ----------
    public async Task InitializeAsync(StudentListItem student)
    {
        _student = student;
        _studentId = student.Id;
        ContextText = $"الطالب: {student.FullName}";

        await using var scope = _scopeFactory.CreateAsyncScope();
        var treasuryHandler = scope.ServiceProvider.GetRequiredService<GetTreasuryAccountsHandler>();
        var treasuryResult = await treasuryHandler.ExecuteAsync(true);
        if (!treasuryResult.IsSuccess) { _notifier.ShowError(treasuryResult.ErrorMessage!); CloseRequested?.Invoke(this, false); return; }
        TreasuryAccounts.Clear(); foreach (var a in treasuryResult.Value!) TreasuryAccounts.Add(a); SelectedTreasuryAccount = TreasuryAccounts.FirstOrDefault();

        var handler = scope.ServiceProvider.GetRequiredService<GetPaymentContextHandler>();
        var result = await handler.ExecuteAsync(student.Id);

        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            CloseRequested?.Invoke(this, false);
            return;
        }

        var context = result.Value!;
        _currentYearOpenCharges = context.CurrentYearOpenCharges;
        _otherYearsOpenCharges = context.OtherYearsOpenCharges;
        _openCharges = _currentYearOpenCharges;
        _guardianPersonId = context.GuardianPersonId;
        _academicYearText = context.CurrentAcademicYearName;
        OnPropertyChanged(nameof(AcademicYearText));
        OnPropertyChanged(nameof(HasOtherYears));
        OnPropertyChanged(nameof(OtherYearsButtonText));

        // D-104/D-36: الولي المسجَّل هو الدافع الغالب — مؤشّر افتراضياً (اسمه من بطاقة الطالب نفسها)
        ShowGuardianOption = context.GuardianPersonId is not null && !string.IsNullOrWhiteSpace(student.GuardianFullName);
        if (ShowGuardianOption)
        {
            GuardianOptionText = $"الدافع هو الولي المسجَّل: {student.GuardianFullName}";
            PaidByGuardian = true;
            OnPropertyChanged(nameof(ShowGuardianOption));
        }

        RebuildVisibleCharges();

        CreditText = context.UnallocatedCentimes > 0
            ? $"زائدة دائنة متاحة من قبل: {MoneyInput.FormatDinars(context.UnallocatedCentimes)} دج (D-107)"
            : string.Empty;

        RecomputeUnallocated();
    }

    // D-106: الاقتراح التلقائي (الأقدم أولاً) يملأ «يُخصَّص» — والسيادة للمستخدم بعده
    private void ApplySuggestion()
    {
        if (!MoneyInput.TryParseDinars(AmountText, out var amountCentimes) || amountCentimes <= 0)
        {
            foreach (var row in Rows)
                row.AllocatedText = string.Empty;
            RecomputeUnallocated();
            return;
        }

        var suggestion = new Dictionary<int, long>();
        var remaining = amountCentimes;

        foreach (var item in PaymentAllocationSuggester.Suggest(_currentYearOpenCharges, remaining))
        {
            suggestion[item.ChargeId] = item.AmountCentimes;
            remaining -= item.AmountCentimes;
        }

        if (_showOtherYears && remaining > 0)
        {
            foreach (var item in PaymentAllocationSuggester.Suggest(_otherYearsOpenCharges, remaining))
            {
                suggestion[item.ChargeId] = item.AmountCentimes;
                remaining -= item.AmountCentimes;
            }
        }

        foreach (var row in Rows)
            row.AllocatedText = suggestion.TryGetValue(row.ChargeId, out var suggested)
                ? MoneyInput.FormatDinars(suggested)
                : string.Empty;

        RecomputeUnallocated();
    }

    private void RecomputeUnallocated()
    {
        if (!MoneyInput.TryParseDinars(AmountText, out var amountCentimes) || amountCentimes <= 0)
        {
            UnallocatedFromThisText = string.Empty;
            return;
        }

        var allocated = 0L;
        foreach (var row in Rows)
            if (MoneyInput.TryParseDinars(row.AllocatedText, out var rowAmount))
                allocated += rowAmount;

        var rest = amountCentimes - allocated;
        UnallocatedFromThisText = rest > 0
            ? $"غير مخصص من هذه الدفعة (زائدة دائنة تبقى للطالب): {MoneyInput.FormatDinars(rest)} دج"
            : string.Empty;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!MoneyInput.TryParseDinars(AmountText, out var amountCentimes) || amountCentimes <= 0)
        {
            ErrorMessage = "أدخل مبلغ القبض بالدينار — أكبر من صفر.";
            return;
        }
        if (PaidOn is null)
        {
            ErrorMessage = "اختر تاريخ القبض.";
            return;
        }
        if (SelectedTreasuryAccount is null) { ErrorMessage = "اختر الحساب المالي."; return; }
        if (PaidOn.Value.Date > DateTime.Today)
        {
            ErrorMessage = "تاريخ القبض لا يمكن أن يكون في المستقبل.";
            return;
        }

        var allocations = new List<PaymentAllocationInput>();
        foreach (var row in Rows)
        {
            if (string.IsNullOrWhiteSpace(row.AllocatedText))
                continue;   // فارغ = لا تخصيص لهذا المستحق
            if (!MoneyInput.TryParseDinars(row.AllocatedText, out var rowAmount) || rowAmount <= 0)
            {
                ErrorMessage = $"مبلغ التخصيص غير صحيح في سطر «{row.SourceText}» — والفارغ يعني بلا تخصيص.";
                return;
            }
            allocations.Add(new PaymentAllocationInput(row.ChargeId, rowAmount));
        }

        var allocatedTotal = allocations.Sum(a => a.AmountCentimes);
        if (allocatedTotal > amountCentimes)
        {
            ErrorMessage = $"مجموع التخصيصات ({MoneyInput.FormatDinars(allocatedTotal)} دج) يتجاوز المبلغ المقبوض — قلّص سطراً.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<RegisterPaymentHandler>();
            var result = await handler.ExecuteAsync(new RegisterPaymentRequest(
                _studentId,
                PaidByGuardian ? _guardianPersonId : null,
                SelectedTreasuryAccount.Id,
                amountCentimes,
                DateOnly.FromDateTime(PaidOn.Value),
                string.IsNullOrWhiteSpace(Note) ? null : Note,
                allocations));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"قُبض {MoneyInput.FormatDinars(amountCentimes)} دج — إيصال #{result.Value:000000} ✔");
                await AskAndPrintReceiptAsync(scope, result.Value, amountCentimes, allocations);   // 6.3 (ط-هـ): «طباعة الآن؟»
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // حُراس القبض ← البانر (D-22)
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// 6.3 (ط-هـ): «طباعة الآن؟» بعد القبض الناجح — يُركَّب نموذج الإيصال من بيانات الديالوغ المؤكَّدة نفسها (WYSIWYP — ط-9):
    /// handler القبض يعيد رقم الإيصال لا معرف الدفعة (مثبَّت باختباراته) وعقود القراءة مثبَّتة، فالتركيب المحلي هو المسار الوحيد بلا مساس بالمثبَّت —
    /// والأوصاف من قائمة المستحقات المعروضة للمستخدم لحظة الحفظ (روح D-128) · إعادة الطباعة لاحقاً تمر بالمعالج النقي من سجل المدفوعات.
    /// فشل الطباعة لا يفسد نجاح القبض أبداً — تحذير يدل على إعادة الطباعة من السجل.
    /// </summary>
    private async Task AskAndPrintReceiptAsync(AsyncServiceScope scope, int receiptNo, long amountCentimes,
        IReadOnlyList<PaymentAllocationInput> allocations)
    {
        if (!await _dialogService.ConfirmAsync(
                "تم القبض ✔",
                $"إيصال قبض #{receiptNo:000000} — {MoneyInput.FormatDinars(amountCentimes)} دج\n\nهل تريد طباعة الإيصال الآن؟",
                "🖨 طباعة"))
            return;

        try
        {
            var school = await scope.ServiceProvider.GetRequiredService<GetSchoolInfoHandler>().ExecuteAsync();
            var info = school.IsSuccess ? school.Value! : new SchoolInfoItem(0, string.Empty, null, null, null);
            var header = new PrintHeader(info.DisplayName, info.Phone, info.Address, info.LogoPath);

            var lines = allocations
                .Select(a => new ReceiptAllocationPrintLine(
                    Rows.First(r => r.ChargeId == a.ChargeId).SourceText, a.AmountCentimes))
                .ToList();

            var model = new ReceiptPrintModel(
                header, PaymentKind.Receipt, receiptNo, PaidOn!.Value,
                _student.FullName, PaidByGuardian ? _student.GuardianFullName : null,
                amountCentimes, string.IsNullOrWhiteSpace(Note) ? null : Note, lines);

            if (_printService.PrintReceipt(model) == PrintOutcome.Failed)
                _notifier.ShowWarning("تعذّرت الطباعة — يمكنك إعادة طباعة الإيصال في أي وقت من سجل المدفوعات في شاشة «💰 المالية» (زر 🖨 على السطر).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print receipt {ReceiptNo} right after payment", receiptNo);
            _notifier.ShowWarning("تعذّرت الطباعة — يمكنك إعادة طباعة الإيصال في أي وقت من سجل المدفوعات في شاشة «💰 المالية» (زر 🖨 على السطر).");
        }
    }
}
