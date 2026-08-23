using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Scheduling;

/// <summary>توليد الحصص من الجدول (D-87): فترة افتراضها اليوم ← +28 يوماً · كل الأفواج أو فوج واحد · آمن لإعادة الضغط</summary>
public sealed class GenerateSessionsDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    public GenerateSessionsDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    public string Title => "توليد الحصص من الجدول";

    public sealed record GroupFilterOption(int? Id, string Label);

    public ObservableCollection<GroupFilterOption> GroupFilters { get; } = new();

    private GroupFilterOption? _selectedGroupFilter;
    public GroupFilterOption? SelectedGroupFilter
    {
        get => _selectedGroupFilter;
        set => SetProperty(ref _selectedGroupFilter, value);
    }

    private DateTime _fromDate = DateTime.Today;
    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    private DateTime _toDate = DateTime.Today.AddDays(28);   // D-87: أربعة أسابيع افتراضياً
    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

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

    public void Initialize()
    {
        FromDate = DateTime.Today;
        ToDate = DateTime.Today.AddDays(28);
        ErrorMessage = null;
        _ = LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetClassGroupsHandler>();
        var result = await handler.ExecuteAsync(null, null);
        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            return;
        }

        GroupFilters.Clear();
        GroupFilters.Add(new GroupFilterOption(null, "كل الأفواج"));
        foreach (var group in result.Value!.Where(g => g.IsActive))
            GroupFilters.Add(new GroupFilterOption(group.Id, $"{group.Name} — {group.AcademicYearName}"));

        SelectedGroupFilter = GroupFilters.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (ToDate.Date < FromDate.Date)
        {
            ErrorMessage = "تاريخ النهاية قبل تاريخ البداية.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GenerateSessionsHandler>();
            var result = await handler.ExecuteAsync(new GenerateSessionsRequest(FromDate, ToDate, SelectedGroupFilter?.Id));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess(result.Value > 0
                    ? $"وُلّدت {result.Value} حصة ✔ (الموجود مسبقاً تُجاوِزه التوليد بأمان)"
                    : "لا جديد — كل حصص الفترة مولَّدة مسبقاً ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;
        }
        finally
        {
            IsSaving = false;
        }
    }
}