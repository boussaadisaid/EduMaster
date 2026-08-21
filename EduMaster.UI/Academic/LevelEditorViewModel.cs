using EduMaster.Application.Academic;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Academic;

public sealed class LevelEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int? _editingId;

    public LevelEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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
    public string Title => _editingId is null ? "مستوى جديد" : "تعديل المستوى";

    private string _name = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _sortOrderText = "0";
    public string SortOrderText { get => _sortOrderText; set => SetProperty(ref _sortOrderText, value); }

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

    public void InitializeForCreate(int suggestedSortOrder)
    {
        _editingId = null;
        SortOrderText = suggestedSortOrder.ToString();
    }

    public void InitializeForEdit(Level level)
    {
        _editingId = level.Id;
        Name = level.Name;
        SortOrderText = level.SortOrder.ToString();
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "أدخل اسم المستوى.";
            return;
        }
        if (!int.TryParse(SortOrderText, out var sortOrder))
        {
            ErrorMessage = "أدخل رقماً صحيحاً لترتيب العرض.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateLevelHandler>();
                var result = await handler.ExecuteAsync(new CreateLevelRequest(Name, sortOrder));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيف المستوى بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateLevelHandler>();
                var result = await handler.ExecuteAsync(new UpdateLevelRequest(_editingId.Value, Name, sortOrder));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظ المستوى ✔"))
                    return;
            }

            CloseRequested?.Invoke(this, true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool HandleResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            return true;
        }

        if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            ErrorMessage = errorMessage;   // Conflict/Validation ← بانر (D-22)

        return false;
    }
}