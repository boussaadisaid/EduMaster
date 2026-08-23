using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Scheduling;

/// <summary>حصة استثنائية في أي وقت (D-87 — بلا مصدر) · تُسمح بأثر رجعي لتوثيق حصة فاتت جدولتها</summary>
public sealed class AdHocSessionViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    public AdHocSessionViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    public string Title => "حصة استثنائية";

    public sealed record GroupOption(int Id, string Label);
    public ObservableCollection<GroupOption> Groups { get; } = new();

    private GroupOption? _selectedGroup;
    public GroupOption? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    private DateTime _date = DateTime.Today;
    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    private string _startTimeText = string.Empty;
    public string StartTimeText
    {
        get => _startTimeText;
        set => SetProperty(ref _startTimeText, value);
    }

    private string _durationText = "90";
    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    private string _topic = string.Empty;
    public string Topic
    {
        get => _topic;
        set => SetProperty(ref _topic, value);
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
        Date = DateTime.Today;
        StartTimeText = string.Empty;
        DurationText = "90";
        Topic = string.Empty;
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

        Groups.Clear();
        foreach (var group in result.Value!.Where(g => g.IsActive))
            Groups.Add(new GroupOption(group.Id, $"{group.Name} — {group.AcademicYearName}"));
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedGroup is null)
        {
            ErrorMessage = "اختر الفوج.";
            return;
        }
        // توقيع ثلاثي كافٍ — صيغة الأرقام لا ثقافة فيها
        if (!TimeOnly.TryParseExact(StartTimeText.Trim(), new[] { "HH:mm", "H:mm" }, out var startTime))
        {
            ErrorMessage = "أدخل الساعة بصيغة 24 ساعة — مثل 08:00 أو 17:30.";
            return;
        }
        if (!int.TryParse(DurationText.Trim(), out var duration) || duration <= 0 || duration > 600)
        {
            ErrorMessage = "المدة بالدقائق يجب أن تكون رقماً بين 1 و600.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateAdHocSessionHandler>();
            var result = await handler.ExecuteAsync(new CreateAdHocSessionRequest(
                SelectedGroup.Id,
                Date.Date.Add(startTime.ToTimeSpan()),
                duration,
                string.IsNullOrWhiteSpace(Topic) ? null : Topic));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess("بُرمجت الحصة الاستثنائية ✔");
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