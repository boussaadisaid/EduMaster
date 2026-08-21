using EduMaster.Application.Academic;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Academic;

public sealed class SubjectEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int? _editingId;

    public SubjectEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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
    public string Title => _editingId is null ? "مادة جديدة" : "تعديل المادة";

    private string _name = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }

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

    public void InitializeForCreate()
    {
        _editingId = null;
    }

    public void InitializeForEdit(Subject subject)
    {
        _editingId = subject.Id;
        Name = subject.Name;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "أدخل اسم المادة.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
                var result = await handler.ExecuteAsync(new CreateSubjectRequest(Name));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيفت المادة بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateSubjectHandler>();
                var result = await handler.ExecuteAsync(new UpdateSubjectRequest(_editingId.Value, Name));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت المادة ✔"))
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