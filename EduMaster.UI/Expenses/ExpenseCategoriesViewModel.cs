using EduMaster.Application.Expenses;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Expenses;

public sealed class ExpenseCategoriesViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int? _editingId;

    public ExpenseCategoriesViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory; _notifier = notifier;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        EditCommand = new AsyncRelayCommand(EditAsync, () => Selected is not null);
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => Selected is not null);
        CloseCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, true); return Task.CompletedTask; });
    }
    public event EventHandler<bool>? CloseRequested;
    public ObservableCollection<ExpenseCategoryItem> Categories { get; } = new();
    private ExpenseCategoryItem? _selected;
    public ExpenseCategoryItem? Selected { get => _selected; set { SetProperty(ref _selected, value); EditCommand.RaiseCanExecuteChanged(); ToggleCommand.RaiseCanExecuteChanged(); } }
    private string _name = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    private string? _error;
    public string? ErrorMessage { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasErrorMessage)); } }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    private bool _isSaving;
    public bool IsSaving { get => _isSaving; private set { SetProperty(ref _isSaving, value); SaveCommand.RaiseCanExecuteChanged(); } }
    public bool IsEditing => _editingId is not null;
    public bool IsEmpty => Categories.Count == 0;
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand ToggleCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }

    public async Task InitializeAsync() { await LoadAsync(); ResetEditor(); }
    private async Task LoadAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetExpenseCategoriesHandler>().ExecuteAsync();
        if (!result.IsSuccess) { _notifier.ShowError(result.ErrorMessage!); return; }
        Categories.Clear();
        foreach (var c in result.Value!)
            Categories.Add(c);
        OnPropertyChanged(nameof(IsEmpty));
    }
    private void ResetEditor()
    {
        _editingId = null; Name = string.Empty; ErrorMessage = null; OnPropertyChanged(nameof(IsEditing));
    }
    private Task EditAsync()
    {
        if (Selected is null) return Task.CompletedTask;
        _editingId = Selected.Id; Name = Selected.Name; ErrorMessage = null; OnPropertyChanged(nameof(IsEditing));
        return Task.CompletedTask;
    }
    private async Task SaveAsync()
    {
        ErrorMessage = null; if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "أدخل اسم الفئة."; return; }
        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            if (_editingId is null)
            {
                var r = await scope.ServiceProvider.GetRequiredService<CreateExpenseCategoryHandler>().ExecuteAsync(new CreateExpenseCategoryRequest(Name));
                if (!r.IsSuccess) { ErrorMessage = r.ErrorMessage; return; }
            }
            else
            {
                var r = await scope.ServiceProvider.GetRequiredService<UpdateExpenseCategoryHandler>().ExecuteAsync(new UpdateExpenseCategoryRequest(_editingId.Value, Name));
                if (!r.IsSuccess) { ErrorMessage = r.ErrorMessage; return; }
            }
            _notifier.ShowSuccess(_editingId is null ? "تم إنشاء الفئة." : "تم تعديل الفئة.");
            await LoadAsync(); ResetEditor();
        }
        finally { IsSaving = false; }
    }
    private async Task ToggleAsync()
    {
        if (Selected is null) return;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = Selected.IsActive
            ? await scope.ServiceProvider.GetRequiredService<DeactivateExpenseCategoryHandler>().ExecuteAsync(new DeactivateExpenseCategoryRequest(Selected.Id))
            : await scope.ServiceProvider.GetRequiredService<ActivateExpenseCategoryHandler>().ExecuteAsync(new ActivateExpenseCategoryRequest(Selected.Id));
        if (result.IsSuccess) { await LoadAsync(); }
        else _notifier.ShowWarning(result.ErrorMessage!);
    }
}
