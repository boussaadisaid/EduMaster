using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EduMaster.UI.Scheduling;

/// <summary>
/// ديالوغ الحضور (D-94): نشطو الفوج (D-102) بثلاثية لكل صف · يُفتح أول مرة بـ«الكل حاضر» افتراضياً من الـHandler،
/// ويُعاد فتحه على المحفوظ للتصحيح (D-101) · حفظ واحد ذرّي = استبدال كامل في معاملة.
/// </summary>
public sealed class SessionAttendanceDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<SessionAttendanceDialogViewModel> _logger;

    private int _sessionId;

    public SessionAttendanceDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<SessionAttendanceDialogViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;

        MarkAllPresentCommand = new AsyncRelayCommand(() =>
        {
            foreach (var row in Rows)
                row.Status = AttendanceStatus.Present;   // D-94: الحاضر هو القاعدة
            return Task.CompletedTask;
        }, () => Rows.Count > 0 && !IsSaving);

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => Rows.Count > 0 && !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => "حضور الحصة";

    private string _contextText = string.Empty;
    public string ContextText
    {
        get => _contextText;
        private set => SetProperty(ref _contextText, value);
    }

    public ObservableCollection<SessionAttendanceRowViewModel> Rows { get; } = new();

    private string _countsText = string.Empty;
    public string CountsText
    {
        get => _countsText;
        private set => SetProperty(ref _countsText, value);
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
        private set
        {
            SetProperty(ref _isSaving, value);
            SaveCommand.RaiseCanExecuteChanged();
            MarkAllPresentCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncRelayCommand MarkAllPresentCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    /// <summary>يُستدعى ويُنتظَر قبل فتح الديالوغ — contextText مثل: «رياضيات — سنة ثالثة · السبت 2026-08-29 · 08:00»</summary>
    public async Task InitializeAsync(int classSessionId, string contextText)
    {
        _sessionId = classSessionId;
        ContextText = contextText;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetSessionAttendanceHandler>();
        var result = await handler.ExecuteAsync(classSessionId);

        if (!result.IsSuccess)
        {
            // تعذّر التحميل (أو الحصة ليست مُقامة — D-100) ← إشعار ولا يُفتح الديالوغ أصلاً
            _notifier.ShowError(result.ErrorMessage!);
            CloseRequested?.Invoke(this, false);
            return;
        }

        Rows.Clear();
        foreach (var item in result.Value!)
        {
            var row = new SessionAttendanceRowViewModel(
                item.ClassGroupEnrollmentId, item.FullName, item.Status, item.Note, RefreshCounts);
            row.PropertyChanged += OnRowPropertyChanged;
            Rows.Add(row);
        }

        RefreshCounts();
        SaveCommand.RaiseCanExecuteChanged();
        MarkAllPresentCommand.RaiseCanExecuteChanged();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionAttendanceRowViewModel.Status))
            RefreshCounts();   // (العداد يتجدد أيضاً عبر ردّ النداء — هذا للوضوح المزدوج)
    }

    private void RefreshCounts()
    {
        var present = Rows.Count(r => r.IsPresent);
        var absent = Rows.Count(r => r.IsAbsent);
        var justified = Rows.Count(r => r.IsJustified);
        CountsText = $"✓ حاضر {present} · ✗ غائب {absent} · ⚠ مبرر {justified}";
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        var entries = Rows
            .Select(r => new SessionAttendanceEntry(
                r.EnrollmentId, r.Status, string.IsNullOrWhiteSpace(r.Note) ? null : r.Note))
            .ToList();

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SaveSessionAttendanceHandler>();
            var result = await handler.ExecuteAsync(new SaveSessionAttendanceRequest(_sessionId, entries));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"حُفظ حضور {result.Value} طالب ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;   // قواعد متوقعة (ليست مُقامة / سطر غريب) ← البانر (D-22)
            }
        }
        catch (Exception ex)   // تحصين إضافي (D-69) — الـHandler يلتقط، وهذا لأي خلل خارج سياقه
        {
            _logger.LogError(ex, "Failed to save attendance for session {ClassSessionId}", _sessionId);
            _notifier.ShowError("تعذّر حفظ الحضور — أعد المحاولة.");
        }
        finally
        {
            IsSaving = false;
        }
    }
}