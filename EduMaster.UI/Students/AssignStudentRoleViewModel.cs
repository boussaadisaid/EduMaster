using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Students;
using EduMaster.Domain.Students;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Students;

public sealed class AssignStudentRoleViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int _personId;
    private int? _guardianId;
    private CancellationTokenSource? _guardianSearchCts;

    public AssignStudentRoleViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && SelectedCategory is not null);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
        ClearGuardianCommand = new AsyncRelayCommand(() =>
        {
            _guardianId = null;
            GuardianName = null;
            OnPropertyChanged(nameof(HasGuardian));
            OnPropertyChanged(nameof(HasNoGuardian));
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    private string _personName = string.Empty;
    public string PersonName { get => _personName; private set => SetProperty(ref _personName, value); }

    public sealed record CategoryOption(StudentCategory Value, string Label);
    public IReadOnlyList<CategoryOption> CategoryOptions { get; } = new[]
    {
        new CategoryOption(StudentCategory.Regular, "نظامي"),
        new CategoryOption(StudentCategory.FreeCandidate, "مترشح حر"),
        new CategoryOption(StudentCategory.University, "جامعي"),
        new CategoryOption(StudentCategory.Training, "تكوين ودورات"),
    };

    private CategoryOption? _selectedCategory;
    public CategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set { SetProperty(ref _selectedCategory, value); SaveCommand.RaiseCanExecuteChanged(); }
    }

    private string _notes = string.Empty;
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    // ---------- منتقي الولي المصغّر (نفس منطق المحرر) ----------
    public ObservableCollection<PersonListItem> GuardianResults { get; } = new();

    private string _guardianSearchText = string.Empty;
    public string GuardianSearchText
    {
        get => _guardianSearchText;
        set
        {
            SetProperty(ref _guardianSearchText, value);
            _ = DebouncedGuardianSearchAsync();
        }
    }

    private string? _guardianName;
    public string? GuardianName
    {
        get => _guardianName;
        private set => SetProperty(ref _guardianName, value);
    }

    public bool HasGuardian => _guardianId is not null;
    public bool HasNoGuardian => _guardianId is null;

    private PersonListItem? _pickedGuardian;
    public PersonListItem? PickedGuardian
    {
        get => _pickedGuardian;
        set
        {
            SetProperty(ref _pickedGuardian, value);
            if (value is null) return;

            _guardianId = value.Id;
            GuardianName = value.FullName;
            OnPropertyChanged(nameof(HasGuardian));
            OnPropertyChanged(nameof(HasNoGuardian));

            GuardianResults.Clear();
            GuardianSearchText = string.Empty;
        }
    }

    private async Task DebouncedGuardianSearchAsync()
    {
        _guardianSearchCts?.Cancel();
        var cts = _guardianSearchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);

            if (string.IsNullOrWhiteSpace(GuardianSearchText))
            {
                GuardianResults.Clear();
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SearchPersonsHandler>();
            var result = await handler.ExecuteAsync(GuardianSearchText, cts.Token);

            if (result.IsSuccess)
            {
                GuardianResults.Clear();
                foreach (var p in result.Value!.Where(p => p.Id != _personId).Take(8))
                    GuardianResults.Add(p);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ---------- الخطأ والحفظ ----------
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
    public AsyncRelayCommand ClearGuardianCommand { get; }

    public void Initialize(PersonListItem person)
    {
        _personId = person.Id;
        PersonName = person.FullName;
        SelectedCategory = CategoryOptions[0];
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedCategory is null)
        {
            ErrorMessage = "اختر صنف الطالب.";
            return;
        }

        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateStudentFileHandler>();
            var result = await handler.ExecuteAsync(
                new CreateStudentFileRequest(_personId, _guardianId, SelectedCategory.Value, Notes));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"أُضيف ملف الطالب لـ«{PersonName}» ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // «له ملف فعّال بالفعل» ← بانر (Conflict)
        }
        finally
        {
            IsBusy = false;
        }
    }
}