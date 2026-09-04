using EduMaster.Application.AcademicYears;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Scheduling;

/// <summary>شاشة «الحصص» (D-94): فلتر تاريخ افتراضه اليوم + فلتر فوج · إقامة وإلغاء (لا خصم) + الحضور للمُقامة (3.3 — D-100)</summary>
public sealed class SessionsViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _loadCts;
    private int? _currentAcademicYearId;

    public SessionsViewModel(IServiceScopeFactory scopeFactory, IServiceProvider services,
        IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        MarkHeldCommand = new AsyncRelayCommand(MarkHeldAsync, () => SelectedSession is { Status: SessionStatus.Scheduled });
        CancelSessionCommand = new AsyncRelayCommand(CancelSessionAsync, () => SelectedSession is { Status: SessionStatus.Scheduled });
        AttendanceCommand = new AsyncRelayCommand(AttendanceAsync, () => SelectedSession is { Status: SessionStatus.Held });   // D-100
        CorrectTeacherCommand = new AsyncRelayCommand(CorrectTeacherAsync, () => SelectedSession is { Status: SessionStatus.Held, TeacherFullName: null });   // 6.6-ص-ب
    }

    // ---------- الفلاتر ----------
    public sealed record GroupFilterOption(int? Id, string Label);
    public ObservableCollection<GroupFilterOption> GroupFilters { get; } = new();

    private GroupFilterOption? _selectedGroupFilter;
    public GroupFilterOption? SelectedGroupFilter
    {
        get => _selectedGroupFilter;
        set
        {
            if (SetProperty(ref _selectedGroupFilter, value))
                Reload();
        }
    }

    private DateTime? _fromDate;
    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
                Reload();
        }
    }

    private DateTime? _toDate;
    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
                Reload();
        }
    }

    private void Reload()
    {
        _loadCts?.Cancel();   // D-64: تبديل الفلاتر فوري — يلغي التحميل السابق
        var cts = _loadCts = new CancellationTokenSource();
        _ = LoadAsync(cts.Token);
    }

    // ---------- الحالة ----------
    public ObservableCollection<ClassSessionListItem> Sessions { get; } = new();

    private ClassSessionListItem? _selectedSession;
    public ClassSessionListItem? SelectedSession
    {
        get => _selectedSession;
        set
        {
            SetProperty(ref _selectedSession, value);
            MarkHeldCommand.RaiseCanExecuteChanged();
            CancelSessionCommand.RaiseCanExecuteChanged();
            AttendanceCommand.RaiseCanExecuteChanged();
            CorrectTeacherCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Sessions.Count == 0;

    // ---------- الأوامر ----------
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand MarkHeldCommand { get; }
    public AsyncRelayCommand CancelSessionCommand { get; }
    public AsyncRelayCommand AttendanceCommand { get; }
    public AsyncRelayCommand CorrectTeacherCommand { get; }   // جديد 6.6-ص-ب

    public async Task InitializeAsync()
    {
        // الافتراضي: اليوم (D-94) — حقلاً مباشرةً بلا إطلاق مزدوج ثم تحميل واحد
        _fromDate = DateTime.Today;
        _toDate = DateTime.Today;
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));

        if (!await LoadCurrentAcademicYearAsync())
            return;

        await LoadGroupFiltersAsync();
        await LoadAsync();
    }

    private async Task<bool> LoadCurrentAcademicYearAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>();
        var result = await handler.ExecuteAsync();
        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            return false;
        }

        _currentAcademicYearId = result.Value!.FirstOrDefault(y => y.IsCurrent)?.Id;
        if (_currentAcademicYearId is null)
        {
            _notifier.ShowWarning("لا توجد سنة دراسية حالية مضبوطة.");
            return false;
        }

        return true;
    }

    private async Task LoadGroupFiltersAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetClassGroupsHandler>();
        var result = await handler.ExecuteAsync(_currentAcademicYearId, null);
        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            return;
        }

        GroupFilters.Clear();
        GroupFilters.Add(new GroupFilterOption(null, "كل الأفواج"));
        foreach (var group in result.Value!)
            GroupFilters.Add(new GroupFilterOption(group.Id, $"{group.Name} — {group.AcademicYearName}"));

        SelectedGroupFilter = GroupFilters.FirstOrDefault();
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetSessionsHandler>();
            var result = await handler.ExecuteAsync(FromDate.Value, ToDate.Value, SelectedGroupFilter?.Id, _currentAcademicYearId, cancellationToken);

            if (result.IsSuccess)
            {
                Sessions.Clear();
                foreach (var session in result.Value!)
                    Sessions.Add(session);

                SelectedSession = SelectedSession is null ? null : Sessions.FirstOrDefault(s => s.Id == SelectedSession.Id);
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException)
        {
            // D-64: إلغاء تحميل سابق عند تبديل الفلاتر — ليس خطأ
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ---------- العمليات ----------
    private async Task MarkHeldAsync()
    {
        var session = SelectedSession;
        if (session is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<MarkSessionHeldHandler>();
        var result = await handler.ExecuteAsync(new MarkSessionHeldRequest(session.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType,
            $"أُقيمت حصة «{session.GroupName}» ✔ — سجّل حضورها بزر «📋 الحضور»");
    }

    // D-100: الحضور للمُقامة فقط — الديالوغ يُحمَّل قبل الفتح ويُفتح على «الكل حاضر» أو المحفوظ (D-94/D-101)
    private async Task AttendanceAsync()
    {
        var session = SelectedSession;
        if (session is null) return;

        var dialog = _services.GetRequiredService<SessionAttendanceDialogViewModel>();
        await dialog.InitializeAsync(session.Id, $"{session.GroupName} · {session.TimeDisplay}");

        // النتيجة true لا تغيّر شبكة الحصص (الحضور لا يمس صفها) — الرصيد يظهر محدَّثاً في شاشة الطلاب
        await _dialogs.ShowDialogAsync(dialog, dialog.Title);
    }

    // 6.6-ص-ب: تصحيح لقطة الأستاذ الفارغة للمُقامة — نجاحه يغيّر صف الشبكة (اسم الأستاذ) فتُعاد التهيئة
    private async Task CorrectTeacherAsync()
    {
        var session = SelectedSession;
        if (session is null) return;

        var dialog = _services.GetRequiredService<CorrectSessionTeacherDialogViewModel>();
        dialog.Initialize(session.Id, $"{session.GroupName} · {session.TimeDisplay}");
        var saved = await _dialogs.ShowDialogAsync(dialog, dialog.Title);
        if (saved)
            await LoadAsync();
    }

    private async Task CancelSessionAsync()
    {
        var session = SelectedSession;
        if (session is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "إلغاء الحصة",
            $"ستُلغى حصة «{session.GroupName}» ({session.TimeDisplay}). الملغاة لا تخصم شيئاً من الأرصدة (D-90) وتبقى في السجل.",
            "إلغاء الحصة");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CancelSessionHandler>();
        var result = await handler.ExecuteAsync(new CancelSessionRequest(session.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُلغيت الحصة ✔");
    }

    private async Task HandleResultAsync(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            await LoadAsync();
        }
        else if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            _notifier.ShowWarning(errorMessage!);
    }
}