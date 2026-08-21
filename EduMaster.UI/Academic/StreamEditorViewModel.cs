using EduMaster.Application.Academic;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Stream = EduMaster.Domain.Academic.Stream;   // احتياط ضد الغموض

namespace EduMaster.UI.Academic;

public sealed class StreamEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int _levelId;
    private int? _editingId;

    public StreamEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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
    public string Title => _editingId is null ? "شعبة جديدة" : "تعديل الشعبة";

    private string _levelName = string.Empty;
    public string LevelName { get => _levelName; private set => SetProperty(ref _levelName, value); }

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

    public void InitializeForCreate(Level level)
    {
        _editingId = null;
        _levelId = level.Id;
        LevelName = level.Name;
    }

    public void InitializeForEdit(Stream stream, string levelName)
    {
        _editingId = stream.Id;
        _levelId = stream.LevelId;
        LevelName = levelName;
        Name = stream.Name;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "أدخل اسم الشعبة.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateStreamHandler>();
                var result = await handler.ExecuteAsync(new CreateStreamRequest(_levelId, Name));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيفت الشعبة بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateStreamHandler>();
                var result = await handler.ExecuteAsync(new UpdateStreamRequest(_editingId.Value, Name));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت الشعبة ✔"))
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
            ErrorMessage = errorMessage;

        return false;
    }
}