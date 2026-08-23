using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Scheduling;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Scheduling;

/// <summary>شراء حصص لتسجيل فوج نشط (D-91/D-99) — كمية بلا مبلغ (D-96: الثمن من سنابشوت التسجيل × العدد)</summary>
public sealed class PurchaseSessionsDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int _enrollmentId;

    public PurchaseSessionsDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    public string Title => "شراء حصص";

    private string _contextText = string.Empty;
    public string ContextText
    {
        get => _contextText;
        private set => SetProperty(ref _contextText, value);
    }

    private string _sessionsCountText = "4";   // عرف الشهر (D-97)
    public string SessionsCountText
    {
        get => _sessionsCountText;
        set => SetProperty(ref _sessionsCountText, value);
    }

    private string _note = string.Empty;
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
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

    public void Initialize(StudentGroupEnrollmentItem enrollment, string studentName)
    {
        _enrollmentId = enrollment.Id;
        ContextText = $"{studentName} — {enrollment.ClassGroupName} · الرصيد الحالي: {enrollment.Balance}";
        SessionsCountText = "4";
        Note = string.Empty;
        ErrorMessage = null;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!int.TryParse(SessionsCountText.Trim(), out var count) || count <= 0)
        {
            ErrorMessage = "عدد الحصص يجب أن يكون رقماً أكبر من صفر.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<PurchaseSessionsHandler>();
            var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(
                _enrollmentId, count, string.IsNullOrWhiteSpace(Note) ? null : Note));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"اشتُري {count} حصص ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // D-99: منسحب ← بانر «أعد إلحاقه أولاً»
        }
        finally
        {
            IsSaving = false;
        }
    }
}