using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Scheduling;

/// <summary>
/// محرر موعد أسبوعي — قبل الحفظ يُفحص التعارض بالقراءة ويُعرض تحذيراً غير مانع (D-89)
/// · والتعديل يلغي الحصص المستقبلية المجدولة (D-88 — يُذكر عددها في النجاح)
/// </summary>
public sealed class ScheduleSlotEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;

    private int? _editingSlotId;   // null = إنشاء
    private int _editGroupId;      // التحرير: الفوج ثابت — يأتي من الموعد نفسه

    public ScheduleSlotEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _dialogs = dialogs;

        for (var day = 1; day <= 7; day++)
            Days.Add(new DayOption(day, SchoolWeek.ArabicName(day)));

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _editingSlotId is null ? "موعد أسبوعي جديد" : "تعديل الموعد";
    public bool IsCreateMode => _editingSlotId is null;
    public bool HasCascadeNote => !IsCreateMode;

    // ---------- الخيارات ----------
    public sealed record GroupOption(int Id, string Label);
    public sealed record DayOption(int Id, string Name);

    public ObservableCollection<GroupOption> Groups { get; } = new();
    public ObservableCollection<DayOption> Days { get; } = new();

    private GroupOption? _selectedGroup;
    public GroupOption? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    private DayOption? _selectedDay;
    public DayOption? SelectedDay
    {
        get => _selectedDay;
        set => SetProperty(ref _selectedDay, value);
    }

    private string _groupFixedText = string.Empty;
    public string GroupFixedText
    {
        get => _groupFixedText;
        private set => SetProperty(ref _groupFixedText, value);
    }

    // ---------- الحقول ----------
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

    // ---------- التهيئة ----------
    public async Task InitializeForCreateAsync()
    {
        _editingSlotId = null;
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(HasCascadeNote));
        await LoadGroupsAsync();
        SelectedDay = Days.FirstOrDefault(d => d.Id == 1);   // السبت افتراضياً — أسبوع المدرسة (D-86)
    }

    public void InitializeForEdit(ScheduleSlotItem slot)
    {
        _editingSlotId = slot.Id;
        _editGroupId = slot.ClassGroupId;
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(HasCascadeNote));

        GroupFixedText = $"{slot.GroupName} — {slot.SubjectName} · {slot.LevelName}";   // الفوج ثابت — الموعد يتبعه
        SelectedDay = Days.FirstOrDefault(d => d.Id == slot.DayOfWeek);
        StartTimeText = slot.StartTime.ToString("HH:mm");
        DurationText = slot.DurationMinutes.ToString();
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
            Groups.Add(new GroupOption(group.Id, $"{group.Name} — {group.AcademicYearName}"));   // D-63: تسمية من خصائص موسومة
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (IsCreateMode && SelectedGroup is null)
        {
            ErrorMessage = "اختر الفوج.";
            return;
        }
        if (SelectedDay is null)
        {
            ErrorMessage = "اختر يوم الأسبوع.";
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

        var classGroupId = IsCreateMode ? SelectedGroup!.Id : _editGroupId;

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            // D-89: فحص التعارض بالقراءة أولاً — تحذير غير مانع بتأكيد المستخدم
            var conflictsHandler = scope.ServiceProvider.GetRequiredService<GetScheduleConflictsHandler>();
            var conflictsResult = await conflictsHandler.ExecuteAsync(new GetScheduleConflictsRequest(
                classGroupId, SelectedDay.Id, startTime, duration, _editingSlotId));
            if (!conflictsResult.IsSuccess)
            {
                ErrorMessage = conflictsResult.ErrorMessage;
                return;
            }
            if (conflictsResult.Value!.Count > 0)
            {
                var lines = string.Join("\n", conflictsResult.Value!.Select(c =>
                    $"• {c.DayName} {c.TimeDisplay} — {c.Reason} مشغولة بفوج «{c.GroupName}» ({c.SubjectName})"));
                var proceed = await _dialogs.ConfirmAsync(
                    "تنبيه تعارض (غير مانع)",
                    $"يوجد تعارض مع مواعيد أخرى:\n{lines}\n\nهل تتابع الحفظ رغم ذلك؟",
                    "متابعة رغم التعارض");
                if (!proceed)
                    return;
            }

            if (_editingSlotId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateScheduleSlotHandler>();
                var result = await handler.ExecuteAsync(new CreateScheduleSlotRequest(
                    classGroupId, SelectedDay.Id, startTime, duration));
                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيف الموعد ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateScheduleSlotHandler>();
                var result = await handler.ExecuteAsync(new UpdateScheduleSlotRequest(
                    _editingSlotId.Value, SelectedDay.Id, startTime, duration));

                if (result.IsSuccess)
                {
                    // D-88: يُذكر عدد الملغاة تلقائياً
                    _notifier.ShowSuccess(result.Value > 0
                        ? $"حُفظ الموعد — وأُلغيت {result.Value} حصة مستقبلية مجدولة (ولّدها من جديد بالتوقيت الجديد)"
                        : "حُفظ الموعد ✔");
                }
                else if (result.ErrorType == ErrorType.Unexpected)
                {
                    _notifier.ShowError(result.ErrorMessage!);
                    return;
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                    return;
                }
            }

            CloseRequested?.Invoke(this, true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // D-22 داخل الديالوغ: المتوقع ← بانر أحمر · غير المتوقع ← Toast
    private bool HandleSaveResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
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