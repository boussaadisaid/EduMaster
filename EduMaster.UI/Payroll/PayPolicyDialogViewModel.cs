using EduMaster.Application.AcademicYears;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;

namespace EduMaster.UI.Payroll;

/// <summary>
/// ديالوغ سياسات الأجر لمستفيد واحد (F5 — دفعة B-2 / D-113/D-114):
/// قائمة سياساته (افتراضية + تجاوزات) + محرر مضمّن · القيمة بالدينار (D-51/D-67) ·
/// النسبة لنوع «نسبة مئوية» فقط · علم الغياب غير المبرر ومنتقي الفوج للأساتذة فقط · النطاق ثابت في التحرير (روح D-61).
/// </summary>
public sealed class PayPolicyDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;

    private PayeeKind _payeeKind;
    private int _payeeId;
    private int? _editingPolicyId;   // null = إنشاء

    public PayPolicyDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _dialogs = dialogs;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        NewCommand = new AsyncRelayCommand(() =>
        {
            ResetEditor();
            return Task.CompletedTask;
        });
        EditPolicyCommand = new AsyncRelayCommand(EditPolicyAsync, () => SelectedPolicy is not null);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => SelectedPolicy is not null);
        CloseCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => "سياسة الأجر";

    // ---------- صف الشبكة (مرآة نمط صفوف Billing) ----------
    public sealed record PolicyRow(PayPolicyItem Item)
    {
        public int Id => Item.Id;
        public string ScopeText => Item.ScopeText;
        public string KindText => Item.KindText;
        public string ValueText => Item.Kind == PayPolicyKind.Percentage
            ? $"{Item.Percentage}%"
            : MoneyInput.FormatDinars(Item.RateCentimes) + " دج";
        public string FlagText => Item.PayeeKind == PayeeKind.Teacher && Item.CountsUnjustifiedAbsent
            ? "يُحتسب الغياب غير المبرر"
            : "—";
        public bool IsActive => Item.IsActive;
        public string StateText => IsActive ? "فعّالة" : "معطّلة";
    }

    // ---------- خيارات المحرر ----------
    public sealed record KindOption(PayPolicyKind Value, string Label);
    public sealed record ScopeOption(int? ClassGroupId, string Label);

    public ObservableCollection<KindOption> KindOptions { get; } = new();
    public ObservableCollection<ScopeOption> ScopeOptions { get; } = new();

    // ---------- الحالة ----------
    private string _payeeName = string.Empty;
    public string PayeeName { get => _payeeName; private set => SetProperty(ref _payeeName, value); }

    public bool IsTeacher => _payeeKind == PayeeKind.Teacher;
    public bool IsCreateMode => _editingPolicyId is null;

    public ObservableCollection<PolicyRow> Policies { get; } = new();

    private PolicyRow? _selectedPolicy;
    public PolicyRow? SelectedPolicy
    {
        get => _selectedPolicy;
        set
        {
            SetProperty(ref _selectedPolicy, value);
            EditPolicyCommand.RaiseCanExecuteChanged();
            ToggleActiveCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsEmpty => Policies.Count == 0;

    private KindOption? _selectedKind;
    public KindOption? SelectedKind
    {
        get => _selectedKind;
        set
        {
            SetProperty(ref _selectedKind, value);
            OnPropertyChanged(nameof(IsPercentageKind));
            OnPropertyChanged(nameof(IsRateKind));
        }
    }

    public bool IsPercentageKind => SelectedKind?.Value == PayPolicyKind.Percentage;
    public bool IsRateKind => SelectedKind is not null && !IsPercentageKind;

    private ScopeOption? _selectedScope;
    public ScopeOption? SelectedScope
    {
        get => _selectedScope;
        set => SetProperty(ref _selectedScope, value);
    }

    private string _rateText = string.Empty;
    public string RateText { get => _rateText; set => SetProperty(ref _rateText, value); }

    private string _percentageText = string.Empty;
    public string PercentageText { get => _percentageText; set => SetProperty(ref _percentageText, value); }

    private bool _countsUnjustifiedAbsent;
    public bool CountsUnjustifiedAbsent { get => _countsUnjustifiedAbsent; set => SetProperty(ref _countsUnjustifiedAbsent, value); }

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
    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand EditPolicyCommand { get; }
    public AsyncRelayCommand ToggleActiveCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }

    // ---------- التهيئة ----------
    public async Task InitializeAsync(PayeeKind payeeKind, int payeeId, string payeeName)
    {
        _payeeKind = payeeKind;
        _payeeId = payeeId;
        PayeeName = payeeName;
        OnPropertyChanged(nameof(IsTeacher));

        KindOptions.Clear();
        if (IsTeacher)
        {
            KindOptions.Add(new KindOption(PayPolicyKind.PerPresentStudent, "لكل حاضر"));
            KindOptions.Add(new KindOption(PayPolicyKind.Percentage, "نسبة مئوية"));
            KindOptions.Add(new KindOption(PayPolicyKind.PerHour, "بالساعة"));
            await LoadScopeOptionsAsync();
        }
        else
        {
            KindOptions.Add(new KindOption(PayPolicyKind.PerDay, "باليوم"));
            KindOptions.Add(new KindOption(PayPolicyKind.PerMonth, "شهري ثابت"));
            // الموظف سياسة واحدة بلا نطاق (D-113) — لا منتقي فوج
        }

        ResetEditor();
        await LoadPoliciesAsync();
    }

    private void ResetEditor()
    {
        _editingPolicyId = null;
        OnPropertyChanged(nameof(IsCreateMode));
        SelectedKind = KindOptions.FirstOrDefault();
        SelectedScope = ScopeOptions.FirstOrDefault();   // «افتراضية»
        RateText = string.Empty;
        PercentageText = string.Empty;
        CountsUnjustifiedAbsent = false;                 // الافتراضي: لا يُحتسب (D-114)
        ErrorMessage = null;
    }

    private async Task LoadScopeOptionsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // السنة الحالية افتراضياً (D-58) — وعند غيابها تُعرض أفواجه عبر كل السنوات
        int? yearId = null;
        var yearsResult = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
        if (yearsResult.IsSuccess)
            yearId = yearsResult.Value!.FirstOrDefault(y => y.IsCurrent)?.Id;

        var groupsResult = await scope.ServiceProvider.GetRequiredService<GetClassGroupsHandler>()
            .ExecuteAsync(yearId, null);
        if (!groupsResult.IsSuccess)
        {
            _notifier.ShowError(groupsResult.ErrorMessage!);
            return;
        }

        ScopeOptions.Clear();
        ScopeOptions.Add(new ScopeOption(null, "افتراضية (كل أفواجه)"));
        foreach (var group in groupsResult.Value!.Where(g => g.TeacherId == _payeeId && g.IsActive))
            ScopeOptions.Add(new ScopeOption(group.Id, group.Name));
    }

    private async Task LoadPoliciesAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetPayPoliciesHandler>();
        var result = await handler.ExecuteAsync(new GetPayPoliciesRequest(_payeeKind, _payeeId));

        if (result.IsSuccess)
        {
            Policies.Clear();
            foreach (var item in result.Value!)
                Policies.Add(new PolicyRow(item));
            OnPropertyChanged(nameof(IsEmpty));
            SelectedPolicy = null;
        }
        else
        {
            _notifier.ShowError(result.ErrorMessage!);
        }
    }

    // ---------- العمليات ----------
    private Task EditPolicyAsync()
    {
        var row = SelectedPolicy;
        if (row is null) return Task.CompletedTask;

        _editingPolicyId = row.Id;
        OnPropertyChanged(nameof(IsCreateMode));

        SelectedKind = KindOptions.FirstOrDefault(k => k.Value == row.Item.Kind);
        RateText = row.Item.Kind == PayPolicyKind.Percentage ? string.Empty : MoneyInput.FormatDinars(row.Item.RateCentimes);
        PercentageText = row.Item.Percentage?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        CountsUnjustifiedAbsent = row.Item.CountsUnjustifiedAbsent;

        // النطاق ثابت في التحرير — والقيمة الحالية تُضمَّن ولو خرجت من القائمة (نمط محرر السعر)
        if (IsTeacher && row.Item.ClassGroupId is not null
            && ScopeOptions.All(o => o.ClassGroupId != row.Item.ClassGroupId))
            ScopeOptions.Add(new ScopeOption(row.Item.ClassGroupId, (row.Item.ClassGroupName ?? "فوج") + " (قديم/معطّل)"));
        SelectedScope = ScopeOptions.FirstOrDefault(o => o.ClassGroupId == row.Item.ClassGroupId) ?? ScopeOptions.FirstOrDefault();

        ErrorMessage = null;
        return Task.CompletedTask;
    }

    private async Task ToggleActiveAsync()
    {
        var row = SelectedPolicy;
        if (row is null) return;

        if (row.IsActive)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "تعطيل السياسة",
                $"ستتوقف السياسة «{row.KindText} — {row.ScopeText}» عن الاستعمال في الاحتسابات القادمة دون حذف شيء — ويمكن إعادة تفعيلها في أي وقت.",
                "تعطيل");
            if (!confirmed) return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetPayPolicyActiveHandler>();
        var result = await handler.ExecuteAsync(new SetPayPolicyActiveRequest(row.Id, !row.IsActive));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess(row.IsActive ? "عُطّلت السياسة" : "فُعّلت السياسة ✔");
            await LoadPoliciesAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);   // Conflict: «توجد سياسة فعّالة أخرى على نفس النطاق — عطّلها أولاً.»
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedKind is null)
        {
            ErrorMessage = "اختر نوع السياسة.";
            return;
        }

        // قاعدة «قيمة أو نسبة — واحدة فقط» بصيغة الواجهة (والكيان يحرسها خلفياً)
        long rateCentimes = 0;
        decimal? percentage = null;
        if (SelectedKind.Value == PayPolicyKind.Percentage)
        {
            var normalizedPercent = PercentageText.Trim().Replace(',', '.').Replace('٫', '.');
            if (!decimal.TryParse(normalizedPercent, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                || parsed <= 0 || parsed > 100)
            {
                ErrorMessage = "أدخل نسبة صحيحة بين 0 و100 (مثل 60 أو 12.5).";
                return;
            }
            percentage = parsed;
        }
        else
        {
            if (!MoneyInput.TryParseDinars(RateText, out rateCentimes) || rateCentimes <= 0)
            {
                ErrorMessage = "أدخل قيمة صحيحة بالدينار أكبر من صفر (مثل 200 أو 1500.50).";
                return;
            }
        }

        var flag = IsTeacher && CountsUnjustifiedAbsent;   // العلم لسياسات الأساتذة فقط (D-114)

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingPolicyId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreatePayPolicyHandler>();
                var result = await handler.ExecuteAsync(new CreatePayPolicyRequest(
                    _payeeKind,
                    TeacherId: IsTeacher ? _payeeId : null,
                    EmployeeId: IsTeacher ? null : _payeeId,
                    ClassGroupId: IsTeacher ? SelectedScope?.ClassGroupId : null,
                    SelectedKind.Value, rateCentimes, percentage, flag));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيفت السياسة ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdatePayPolicyHandler>();
                var result = await handler.ExecuteAsync(new UpdatePayPolicyRequest(
                    _editingPolicyId.Value, SelectedKind.Value, rateCentimes, percentage, flag));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت السياسة ✔"))
                    return;
            }

            ResetEditor();
            await LoadPoliciesAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    // D-22 داخل الديالوغ: المتوقع ← بانر أحمر · غير المتوقع ← Toast
    private bool HandleSaveResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            return true;
        }

        if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            ErrorMessage = errorMessage;   // Conflict الفرادة ورسائل الكيان تظهر هنا نظيفة

        return false;
    }
}