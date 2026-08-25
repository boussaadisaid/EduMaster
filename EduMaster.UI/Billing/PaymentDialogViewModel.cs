using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Students;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Billing;

/// <summary>
/// ديالوغ القبض (D-104…D-107): مبلغ + تاريخ (اليوم افتراضاً) + «الولي المسجَّل هو الدافع» عند وجوده
/// + تخصيص مقترح تلقائياً (الأقدم أولاً) قابل للتعديل + الزائدة الدائنة مرئية حيّة.
/// القبض متاح دائماً — الدين لا يموت بتعطيل الطالب.
/// </summary>
public sealed class PaymentDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int _studentId;
    private int? _guardianPersonId;
    private IReadOnlyList<OpenChargeItem> _openCharges = new List<OpenChargeItem>();

    public PaymentDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    public bool HasOpenCharges => Rows.Count > 0;

    public bool NoOpenCharges => Rows.Count == 0;

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

        var context = result.Value!;
        _openCharges = context.OpenCharges;
        _guardianPersonId = context.GuardianPersonId;

        // D-104/D-36: الولي المسجَّل هو الدافع الغالب — مؤشّر افتراضياً (اسمه من بطاقة الطالب نفسها)
        ShowGuardianOption = context.GuardianPersonId is not null && !string.IsNullOrWhiteSpace(student.GuardianFullName);
        if (ShowGuardianOption)
        {
            GuardianOptionText = $"الدافع هو الولي المسجَّل: {student.GuardianFullName}";
            PaidByGuardian = true;
            OnPropertyChanged(nameof(ShowGuardianOption));
        }

        Rows.Clear();
        foreach (var charge in context.OpenCharges)
            Rows.Add(new PaymentAllocationRowViewModel(
                charge.Id, charge.KindText, charge.SourceDescription,
                $"{MoneyInput.FormatDinars(charge.RemainingCentimes)} دج", RecomputeUnallocated));
        OnPropertyChanged(nameof(HasOpenCharges));
        OnPropertyChanged(nameof(NoOpenCharges));

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

        var suggestion = PaymentAllocationSuggester.Suggest(_openCharges, amountCentimes)
            .ToDictionary(s => s.ChargeId, s => s.AmountCentimes);

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
                amountCentimes,
                DateOnly.FromDateTime(PaidOn.Value),
                string.IsNullOrWhiteSpace(Note) ? null : Note,
                allocations));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"قُبض {MoneyInput.FormatDinars(amountCentimes)} دج — إيصال #{result.Value:000000} ✔");
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
}