using EduMaster.Application.Common;
using EduMaster.Application.Employees;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Payroll;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Employees;

/// <summary>شاشة الموظفين (F5 — دفعة B) — قائمة + محرر + سياسة الأجر + لوحة سجل أيام العمل الجانبية (ب-4 المحسوم / مرآة لوحة الطالب D-70) · ب-3+: بوّابة «الشهري الثابت» — لا أيام له (D-113) · التعطيل/التفعيل على الشخص من شاشة الأشخاص (ب-6 / D-31)</summary>
public sealed class EmployeesViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _searchCts;

    public EmployeesViewModel(
        IServiceScopeFactory scopeFactory,
        IServiceProvider services,
        IUserNotifier notifier,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedEmployee is not null);
        RemoveFileCommand = new AsyncRelayCommand(RemoveFileAsync, () => SelectedEmployee is not null);
        OpenPayPolicyCommand = new AsyncRelayCommand(OpenPayPolicyAsync, () => SelectedEmployee is not null);
        AddWorkDayCommand = new AsyncRelayCommand(AddWorkDayAsync, () => SelectedEmployee is not null);
        RemoveWorkDayCommand = new AsyncRelayCommand(RemoveWorkDayAsync, () => SelectedWorkLog is not null);
    }

    // ---------- صف عرض يوم العمل (B-3): WorkDate تصل DateTime من عمود DATE (اتفاق D-112) وتُعرض كتاريخ ----------
    public sealed record WorkLogRow(WorkLogItem Item)
    {
        public int Id => Item.Id;
        public string DateText => Item.WorkDate.ToString("yyyy-MM-dd");
        public string NoteText => string.IsNullOrWhiteSpace(Item.Note) ? "—" : Item.Note;
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            _ = DebouncedSearchAsync();
        }
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);
            await LoadAsync(cts.Token);
        }
        catch (OperationCanceledException) { }   // D-64: الإلغاء ليس خطأً
    }

    public ObservableCollection<EmployeeListItem> Employees { get; } = new();

    private EmployeeListItem? _selectedEmployee;
    public EmployeeListItem? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            SetProperty(ref _selectedEmployee, value);
            EditCommand.RaiseCanExecuteChanged();
            RemoveFileCommand.RaiseCanExecuteChanged();
            OpenPayPolicyCommand.RaiseCanExecuteChanged();
            AddWorkDayCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NoEmployeeSelected));
            OnPropertyChanged(nameof(WorkLogEmpty));
            _ = LoadWorkLogAsync();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Employees.Count == 0;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand RemoveFileCommand { get; }
    public AsyncRelayCommand OpenPayPolicyCommand { get; }
    public AsyncRelayCommand AddWorkDayCommand { get; }
    public AsyncRelayCommand RemoveWorkDayCommand { get; }

    public Task InitializeAsync() => LoadAsync();

    // ---------- لوحة سجل أيام العمل ----------
    public ObservableCollection<WorkLogRow> WorkLog { get; } = new();

    private WorkLogRow? _selectedWorkLog;
    public WorkLogRow? SelectedWorkLog
    {
        get => _selectedWorkLog;
        set
        {
            SetProperty(ref _selectedWorkLog, value);
            RemoveWorkDayCommand.RaiseCanExecuteChanged();
        }
    }

    private DateTime? _newWorkDate = DateTime.Today;   // الافتراضي اليوم — حارس «لا مستقبل» في الـHandler عبر IClock (D-20)
    public DateTime? NewWorkDate { get => _newWorkDate; set => SetProperty(ref _newWorkDate, value); }

    private string _newWorkNote = string.Empty;
    public string NewWorkNote { get => _newWorkNote; set => SetProperty(ref _newWorkNote, value); }

    public bool NoEmployeeSelected => SelectedEmployee is null;
    public bool WorkLogEmpty => SelectedEmployee is not null && WorkLog.Count == 0;

    // ب-3+: بوّابة «الشهري الثابت» — سياسته الفعّالة لا تستهلك أيام العمل (D-113) فننبّه ونُعطّل المدخلات
    private bool _isMonthlyPolicy;
    public bool IsMonthlyPolicy
    {
        get => _isMonthlyPolicy;
        private set { SetProperty(ref _isMonthlyPolicy, value); OnPropertyChanged(nameof(CanLogWorkDays)); }
    }

    public bool CanLogWorkDays => !IsMonthlyPolicy;

    private async Task LoadWorkLogAsync()
    {
        WorkLog.Clear();
        SelectedWorkLog = null;
        IsMonthlyPolicy = false;

        var employee = SelectedEmployee;
        if (employee is null)
        {
            OnPropertyChanged(nameof(WorkLogEmpty));
            return;
        }

        var employeeId = employee.Id;   // حارس السبق: تُقبل النتيجة فقط إن بقي نفس الموظف محدداً (نمط بطاقة الحساب في الأشخاص)
        await using var scope = _scopeFactory.CreateAsyncScope();

        // بوّابة الشهري الثابت: السياسة الفعّالة للموظف ضمن نفس الرحلة
        var policiesResult = await scope.ServiceProvider.GetRequiredService<GetPayPoliciesHandler>()
            .ExecuteAsync(new GetPayPoliciesRequest(PayeeKind.Employee, employeeId));
        if (policiesResult.IsSuccess && SelectedEmployee?.Id == employeeId)
            IsMonthlyPolicy = policiesResult.Value!.Any(p => p.IsActive && p.Kind == PayPolicyKind.PerMonth);

        var handler = scope.ServiceProvider.GetRequiredService<GetWorkLogHandler>();
        var result = await handler.ExecuteAsync(new GetWorkLogRequest(employeeId, null, null));

        if (result.IsSuccess && SelectedEmployee?.Id == employeeId)
        {
            foreach (var item in result.Value!)
                WorkLog.Add(new WorkLogRow(item));
            OnPropertyChanged(nameof(WorkLogEmpty));
        }
        else if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
        }
    }

    private async Task AddWorkDayAsync()
    {
        var employee = SelectedEmployee;
        if (employee is null) return;

        if (NewWorkDate is null)
        {
            _notifier.ShowWarning("اختر تاريخ يوم العمل.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<AddWorkLogDayHandler>();
        var result = await handler.ExecuteAsync(
            new AddWorkLogDayRequest(employee.Id, DateOnly.FromDateTime(NewWorkDate.Value), NewWorkNote));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("سُجّل يوم العمل ✔");
            NewWorkDate = DateTime.Today;
            NewWorkNote = string.Empty;
            await LoadWorkLogAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);   // «مستقبل» / «مسجَّل مسبقاً — احذفه أولاً» (D-29 تحذيري)
    }

    private async Task RemoveWorkDayAsync()
    {
        var row = SelectedWorkLog;
        if (row is null || SelectedEmployee is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "حذف يوم عمل",
            $"سيُحذف يوم {row.DateText} من سجل «{SelectedEmployee.FullName}». التصحيح = حذف اليوم ثم إعادة تسجيله (كتابة فقط — D-115).",
            "احذف اليوم");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RemoveWorkLogDayHandler>();
        var result = await handler.ExecuteAsync(new RemoveWorkLogDayRequest(row.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("حُذف يوم العمل ✔");
            await LoadWorkLogAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetEmployeesHandler>();
            var result = await handler.ExecuteAsync(SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                Employees.Clear();
                foreach (var employee in result.Value!)
                    Employees.Add(employee);

                SelectedEmployee = SelectedEmployee is null ? null : Employees.FirstOrDefault(e => e.Id == SelectedEmployee.Id);
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

    private async Task AddAsync()
    {
        var editor = _services.GetRequiredService<EmployeeEditorViewModel>();
        editor.InitializeForCreate();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedEmployee is null) return;

        var editor = _services.GetRequiredService<EmployeeEditorViewModel>();
        editor.InitializeForEdit(SelectedEmployee);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task RemoveFileAsync()
    {
        var employee = SelectedEmployee;
        if (employee is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "إزالة ملف الموظف",
            $"سيُزال ملف الموظف لـ«{employee.FullName}» (حذف منطقي). الشخص نفسه يبقى في السجل المدني سليماً بكل بياناته.",
            "إزالة الملف");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SoftDeleteEmployeeHandler>();
        var result = await handler.ExecuteAsync(new SoftDeleteEmployeeRequest(employee.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُزيل ملف الموظف ✔");
    }

    // F5 — دفعة B-2: ديالوغ سياسة الأجر للموظف المحدد (باليوم/شهري — D-113)
    private async Task OpenPayPolicyAsync()
    {
        var employee = SelectedEmployee;
        if (employee is null) return;

        var dialog = _services.GetRequiredService<PayPolicyDialogViewModel>();
        await dialog.InitializeAsync(PayeeKind.Employee, employee.Id, employee.FullName);
        await _dialogs.ShowDialogAsync(dialog, dialog.Title);
    }

    // D-22 الموسَّعة (D-29): نجاح ← Toast · متوقع خارج الفورم ← تحذيري · غير متوقع ← خطأ
    private async Task HandleResultAsync(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            await LoadAsync();
        }
        else if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            _notifier.ShowWarning(errorMessage!);
    }
}