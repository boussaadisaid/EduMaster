using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Scheduling;

/// <summary>جدول استعمال الزمن (D-86): سبعة أعمدة (السبت…الجمعة) — بطاقات المواعيد + إدارتها + التوليد والاستثنائية</summary>
public sealed class TimetableViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _loadCts;

    public TimetableViewModel(IServiceScopeFactory scopeFactory, IServiceProvider services,
        IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        for (var day = 1; day <= 7; day++)
            Days.Add(new DayColumn(day));

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        AddSlotCommand = new AsyncRelayCommand(AddSlotAsync);
        EditSlotCommand = new AsyncRelayCommand(EditSlotAsync, () => SelectedSlot is not null);
        DeactivateSlotCommand = new AsyncRelayCommand(DeactivateSlotAsync, () => SelectedSlot is { IsActive: true });
        ActivateSlotCommand = new AsyncRelayCommand(ActivateSlotAsync, () => SelectedSlot is { IsActive: false });
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        AdHocCommand = new AsyncRelayCommand(AdHocAsync);
    }

    // ---------- أعمدة الأيام ----------
    public sealed class DayColumn
    {
        public DayColumn(int dayOfWeek)
        {
            DayOfWeek = dayOfWeek;
            DayName = SchoolWeek.ArabicName(dayOfWeek);
        }

        public int DayOfWeek { get; }
        public string DayName { get; }
        public ObservableCollection<ScheduleSlotItem> Slots { get; } = new();
    }

    public ObservableCollection<DayColumn> Days { get; } = new();

    // ---------- الحالة ----------
    private bool _showInactive;
    public bool ShowInactive
    {
        get => _showInactive;
        set
        {
            if (SetProperty(ref _showInactive, value))
            {
                _loadCts?.Cancel();
                var cts = _loadCts = new CancellationTokenSource();
                _ = LoadAsync(cts.Token);
            }
        }
    }

    private ScheduleSlotItem? _selectedSlot;
    public ScheduleSlotItem? SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            SetProperty(ref _selectedSlot, value);
            EditSlotCommand.RaiseCanExecuteChanged();
            DeactivateSlotCommand.RaiseCanExecuteChanged();
            ActivateSlotCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Days.All(d => d.Slots.Count == 0);

    // ---------- الأوامر ----------
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddSlotCommand { get; }
    public AsyncRelayCommand EditSlotCommand { get; }
    public AsyncRelayCommand DeactivateSlotCommand { get; }
    public AsyncRelayCommand ActivateSlotCommand { get; }
    public AsyncRelayCommand GenerateCommand { get; }
    public AsyncRelayCommand AdHocCommand { get; }

    public Task InitializeAsync() => LoadAsync();

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetTimetableHandler>();
            var result = await handler.ExecuteAsync(ShowInactive, cancellationToken);

            if (result.IsSuccess)
            {
                foreach (var day in Days)
                    day.Slots.Clear();
                foreach (var slot in result.Value!)
                    Days[slot.DayOfWeek - 1].Slots.Add(slot);

                SelectedSlot = null;
                OnPropertyChanged(nameof(IsEmpty));
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException)
        {
            // D-64: تبديل «عرض المعطّلة» بسرعة يلغي التحميل السابق — ليس خطأ
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ---------- العمليات ----------
    private async Task AddSlotAsync()
    {
        var editor = _services.GetRequiredService<ScheduleSlotEditorViewModel>();
        await editor.InitializeForCreateAsync();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task EditSlotAsync()
    {
        var slot = SelectedSlot;
        if (slot is null) return;

        var editor = _services.GetRequiredService<ScheduleSlotEditorViewModel>();
        editor.InitializeForEdit(slot);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task DeactivateSlotAsync()
    {
        var slot = SelectedSlot;
        if (slot is null) return;

        // D-88: الكاسكيد معلَن مسبقاً في التأكيد
        var confirmed = await _dialogs.ConfirmAsync(
            "تعطيل الموعد",
            $"سيُعطَّل موعد «{slot.GroupName}» ({slot.DayName} {slot.TimeDisplay}) وتُلغى حصصه المستقبلية المجدولة تلقائياً — المُقامة والملغاة لا تُمسّ.",
            "تعطيل الموعد");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateScheduleSlotHandler>();
        var result = await handler.ExecuteAsync(new DeactivateScheduleSlotRequest(slot.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess(result.Value > 0
                ? $"عُطّل الموعد — وأُلغيت {result.Value} حصة مستقبلية مجدولة"
                : "عُطّل الموعد ✔");
            await LoadAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task ActivateSlotAsync()
    {
        var slot = SelectedSlot;
        if (slot is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivateScheduleSlotHandler>();
        var result = await handler.ExecuteAsync(new ActivateScheduleSlotRequest(slot.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("فُعّل الموعد ✔ — ولّد الحصص لالتقاطه من جديد");
            await LoadAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task GenerateAsync()
    {
        var dialog = _services.GetRequiredService<GenerateSessionsDialogViewModel>();
        dialog.Initialize();
        await _dialogs.ShowDialogAsync(dialog, dialog.Title);   // نتيجة التوليد تُعلَن داخله
    }

    private async Task AdHocAsync()
    {
        var dialog = _services.GetRequiredService<AdHocSessionViewModel>();
        dialog.Initialize();
        await _dialogs.ShowDialogAsync(dialog, dialog.Title);
    }
}