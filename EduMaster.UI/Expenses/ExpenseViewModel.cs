using EduMaster.Application.AcademicYears;
using EduMaster.Application.Expenses;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Expenses;

public sealed class ExpenseViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ExpenseViewModel> _logger;

    public ExpenseViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs,
        ILogger<ExpenseViewModel> logger)
    {
        _scopeFactory = scopeFactory; _notifier = notifier; _dialogs = dialogs; _logger = logger;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        NewExpenseCommand = new AsyncRelayCommand(NewExpenseAsync);
        EditExpenseCommand = new AsyncRelayCommand(EditExpenseAsync, () => SelectedExpense is not null);
        DeleteExpenseCommand = new AsyncRelayCommand(DeleteExpenseAsync, () => SelectedExpense is not null);
        ManageCategoriesCommand = new AsyncRelayCommand(ManageCategoriesAsync);
    }

    public ObservableCollection<AcademicYearListItem> AcademicYears { get; } = new();
    public ObservableCollection<ExpenseCategoryItem> Categories { get; } = new();
    public ObservableCollection<ExpenseListItem> Expenses { get; } = new();

    private AcademicYearListItem? _selectedAcademicYear;
    public AcademicYearListItem? SelectedAcademicYear
    {
        get => _selectedAcademicYear;
        set { if (SetProperty(ref _selectedAcademicYear, value)) _ = LoadExpensesAsync(); }
    }

    private ExpenseCategoryItem? _selectedCategory;
    public ExpenseCategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                _ = LoadExpensesAsync();
        }
    }

    private DateTime? _fromDate;
    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
                _ = LoadExpensesAsync();
        }
    }

    private DateTime? _toDate;
    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
                _ = LoadExpensesAsync();
        }
    }

    private ExpenseListItem? _selectedExpense;
    public ExpenseListItem? SelectedExpense
    {
        get => _selectedExpense;
        set { SetProperty(ref _selectedExpense, value); EditExpenseCommand.RaiseCanExecuteChanged(); DeleteExpenseCommand.RaiseCanExecuteChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); } }
    public bool IsEmpty => !IsLoading && Expenses.Count == 0;
    public string TotalText => $"الإجمالي: {MoneyInput.FormatDinars(Expenses.Sum(x => x.AmountCentimes))} دج";

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand NewExpenseCommand { get; }
    public AsyncRelayCommand EditExpenseCommand { get; }
    public AsyncRelayCommand DeleteExpenseCommand { get; }
    public AsyncRelayCommand ManageCategoriesCommand { get; }

    public async Task InitializeAsync()
    {
        // نفس سلوك شاشة المالية: الفترة الابتدائية = اليوم، لتفادي placeholder الإنجليزي
        // «Select a date» ولعرض حركة اليوم مباشرة عند فتح الشاشة.
        var today = DateTime.Today;
        _fromDate = today;
        _toDate = today;
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));

        await LoadYearsAsync();
        await LoadCategoriesAsync();
        await LoadExpensesAsync();
    }

    private async Task LoadYearsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
        if (!result.IsSuccess) { _notifier.ShowError(result.ErrorMessage!); return; }
        AcademicYears.Clear(); foreach (var year in result.Value!) AcademicYears.Add(year);
        SelectedAcademicYear = AcademicYears.FirstOrDefault(x => x.IsCurrent && x.IsActive) ?? AcademicYears.FirstOrDefault(x => x.IsActive);
    }

    private async Task LoadCategoriesAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetExpenseCategoriesHandler>().ExecuteAsync();
        if (!result.IsSuccess) { _notifier.ShowError(result.ErrorMessage!); return; }
        var selectedId = SelectedCategory?.Id;

        Categories.Clear();
        Categories.Add(new ExpenseCategoryItem(0, "كل الفئات", true));
        foreach (var item in result.Value!)
            Categories.Add(item);

        _selectedCategory = selectedId is not null
            ? Categories.FirstOrDefault(x => x.Id == selectedId.Value) ?? Categories[0]
            : Categories[0];
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private async Task LoadAsync() { await LoadYearsAsync(); await LoadCategoriesAsync(); await LoadExpensesAsync(); }

    private async Task LoadExpensesAsync()
    {
        if (SelectedAcademicYear is null) return;
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetExpensesHandler>().ExecuteAsync(
                SelectedAcademicYear.Id,
                FromDate is null ? null : DateOnly.FromDateTime(FromDate.Value),
                ToDate is null ? null : DateOnly.FromDateTime(ToDate.Value),
                SelectedCategory?.Id is 0 ? null : SelectedCategory?.Id);
            if (!result.IsSuccess)
            {
                if (result.ErrorType == EduMaster.Application.Common.ErrorType.Unexpected) _notifier.ShowError(result.ErrorMessage!);
                else _notifier.ShowWarning(result.ErrorMessage!);
                return;
            }
            Expenses.Clear();
            foreach (var item in result.Value!) Expenses.Add(item);
            OnPropertyChanged(nameof(TotalText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expenses"); _notifier.ShowError("تعذّر تحميل المصاريف.");
        }
        finally { IsLoading = false; }
    }

    private async Task NewExpenseAsync()
    {
        if (SelectedAcademicYear is null) { _notifier.ShowWarning("اختر السنة الدراسية أولاً."); return; }
        using var scope = _scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ExpenseEditorViewModel>();
        vm.InitializeForCreate(SelectedAcademicYear.Id);
        var saved = await _dialogs.ShowDialogAsync(vm, "مصروف جديد");
        if (saved) await LoadAsync();
    }

    private async Task EditExpenseAsync()
    {
        if (SelectedExpense is null) return;
        using var scope = _scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ExpenseEditorViewModel>();
        vm.InitializeForEdit(SelectedExpense);
        var saved = await _dialogs.ShowDialogAsync(vm, "تعديل المصروف");
        if (saved) await LoadAsync();
    }

    private async Task DeleteExpenseAsync()
    {
        if (SelectedExpense is null) return;
        if (!await _dialogs.ConfirmAsync("حذف المصروف", $"هل تريد حذف المصروف بمبلغ {MoneyInput.FormatDinars(SelectedExpense.AmountCentimes)} دج؟", "حذف المصروف")) return;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<RemoveExpenseHandler>().ExecuteAsync(new RemoveExpenseRequest(SelectedExpense.Id));
        if (result.IsSuccess) { _notifier.ShowSuccess("تم حذف المصروف."); await LoadExpensesAsync(); }
        else _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task ManageCategoriesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ExpenseCategoriesViewModel>();

        // DialogService لا يستدعي InitializeAsync تلقائياً؛ يجب تحميل الفئات قبل عرض النافذة.
        await vm.InitializeAsync();

        var changed = await _dialogs.ShowDialogAsync(vm, "فئات المصاريف");
        if (changed)
            await LoadAsync();
    }
}
