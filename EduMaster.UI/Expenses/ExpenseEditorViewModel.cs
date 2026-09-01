using EduMaster.Application.AcademicYears;
using EduMaster.Application.Expenses;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;

namespace EduMaster.UI.Expenses;

public sealed class ExpenseEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int? _editingId;

    public ExpenseEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory; _notifier = notifier;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, false); return Task.CompletedTask; });
    }
    public event EventHandler<bool>? CloseRequested;
    public string Title => _editingId is null ? "مصروف جديد" : "تعديل المصروف";
    public ObservableCollection<AcademicYearListItem> AcademicYears { get; } = new();
    public ObservableCollection<ExpenseCategoryItem> Categories { get; } = new();

    private AcademicYearListItem? _selectedYear;
    public AcademicYearListItem? SelectedYear { get => _selectedYear; set => SetProperty(ref _selectedYear, value); }
    private ExpenseCategoryItem? _selectedCategory;
    public ExpenseCategoryItem? SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }
    private DateTime? _expenseDate;
    public DateTime? ExpenseDate { get => _expenseDate; set => SetProperty(ref _expenseDate, value); }
    private string _amountText = string.Empty;
    public string AmountText { get => _amountText; set => SetProperty(ref _amountText, value); }
    private string _note = string.Empty;
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); } }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    private bool _isSaving;
    public bool IsSaving { get => _isSaving; private set { SetProperty(ref _isSaving, value); SaveCommand.RaiseCanExecuteChanged(); } }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public void InitializeForCreate(int academicYearId)
    {
        _editingId = null; OnPropertyChanged(nameof(Title));
        _ = LoadOptionsAsync(academicYearId, null, null, DateTime.Today);
    }
    public void InitializeForEdit(ExpenseListItem item)
    {
        _editingId = item.Id; OnPropertyChanged(nameof(Title));
        _ = LoadOptionsAsync(item.AcademicYearId, item.ExpenseCategoryId, item.ExpenseDate, item.ExpenseDate.ToDateTime(TimeOnly.MinValue), item.AmountCentimes, item.Note);
    }
    private async Task LoadOptionsAsync(int yearId, int? categoryId, DateOnly? date, DateTime defaultDate, long? amount = null, string? note = null)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var years = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
        var categories = await scope.ServiceProvider.GetRequiredService<GetExpenseCategoriesHandler>().ExecuteAsync();
        if (years.IsSuccess) { AcademicYears.Clear(); foreach (var y in years.Value!) AcademicYears.Add(y); SelectedYear = AcademicYears.FirstOrDefault(y => y.Id == yearId); }
        if (categories.IsSuccess) { Categories.Clear(); foreach (var c in categories.Value!) Categories.Add(c); SelectedCategory = Categories.FirstOrDefault(c => c.Id == categoryId) ?? Categories.FirstOrDefault(c => c.IsActive); }
        ExpenseDate = date?.ToDateTime(TimeOnly.MinValue) ?? defaultDate; AmountText = amount is null ? string.Empty : MoneyInput.FormatDinars(amount.Value); Note = note ?? string.Empty;
    }
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (SelectedYear is null) { ErrorMessage = "اختر السنة الدراسية."; return; }
        if (SelectedCategory is null || !SelectedCategory.IsActive) { ErrorMessage = "اختر فئة مصروف فعّالة."; return; }
        if (ExpenseDate is null) { ErrorMessage = "اختر تاريخ المصروف."; return; }
        if (!MoneyInput.TryParseDinars(AmountText, out var amount) || amount <= 0) { ErrorMessage = "أدخل مبلغاً صحيحاً أكبر من صفر."; return; }
        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            bool success;
            string? error;
            if (_editingId is null)
            {
                var r = await scope.ServiceProvider.GetRequiredService<AddExpenseHandler>().ExecuteAsync(new AddExpenseRequest(
                    SelectedYear.Id, SelectedCategory.Id, DateOnly.FromDateTime(ExpenseDate.Value), amount, Note));
                success = r.IsSuccess; error = r.ErrorMessage;
            }
            else
            {
                var r = await scope.ServiceProvider.GetRequiredService<UpdateExpenseHandler>().ExecuteAsync(new UpdateExpenseRequest(
                    _editingId.Value, SelectedYear.Id, SelectedCategory.Id, DateOnly.FromDateTime(ExpenseDate.Value), amount, Note));
                success = r.IsSuccess; error = r.ErrorMessage;
            }
            if (success) { _notifier.ShowSuccess(_editingId is null ? "تم تسجيل المصروف." : "تم تعديل المصروف."); CloseRequested?.Invoke(this, true); }
            else ErrorMessage = error;
        }
        catch (Exception) { ErrorMessage = "حدث خطأ غير متوقع أثناء حفظ المصروف."; }
        finally { IsSaving = false; }
    }
}
