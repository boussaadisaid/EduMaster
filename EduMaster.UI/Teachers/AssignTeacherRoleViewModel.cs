using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Teachers;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Teachers;

public sealed class AssignTeacherRoleViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int _personId;

    public AssignTeacherRoleViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    private string _personName = string.Empty;
    public string PersonName { get => _personName; private set => SetProperty(ref _personName, value); }

    private string _specialty = string.Empty;
    public string Specialty { get => _specialty; set => SetProperty(ref _specialty, value); }

    private string _notes = string.Empty;
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public void Initialize(PersonListItem person)
    {
        _personId = person.Id;
        PersonName = person.FullName;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateTeacherFileHandler>();
            var result = await handler.ExecuteAsync(new CreateTeacherFileRequest(_personId, Specialty, Notes));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"أُضيف ملف الأستاذ لـ«{PersonName}» ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }
}