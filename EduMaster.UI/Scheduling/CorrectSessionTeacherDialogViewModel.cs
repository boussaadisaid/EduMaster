using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Application.Teachers;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Scheduling;

/// <summary>ديالوغ تصحيح لقطة الأستاذ (6.6-ص-ب): للمُقامة بلقطة فارغة فقط — اختيار من أقامها فعلاً (الفعّالون — مرآة محرر الفوج) · المتوقع بانر داخلي وغير المتوقع Toast (D-22)</summary>
public sealed class CorrectSessionTeacherDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    public CorrectSessionTeacherDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => SelectedTeacher is not null && !IsSaving);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    public event EventHandler<bool>? CloseRequested;
    public string Title => "تصحيح لقطة الأستاذ";

    private int _sessionId;

    public ObservableCollection<TeacherListItem> Teachers { get; } = new();

    private string _sessionText = string.Empty;
    public string SessionText { get => _sessionText; private set => SetProperty(ref _sessionText, value); }

    private TeacherListItem? _selectedTeacher;
    public TeacherListItem? SelectedTeacher
    {
        get => _selectedTeacher;
        set { SetProperty(ref _selectedTeacher, value); SaveCommand.RaiseCanExecuteChanged(); }
    }

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set { SetProperty(ref _isSaving, value); SaveCommand.RaiseCanExecuteChanged(); }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); }
    }
    public bool HasErrorMessage => ErrorMessage is not null;

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public void Initialize(int sessionId, string sessionText)
    {
        _sessionId = sessionId;
        SessionText = sessionText;
        _ = LoadTeachersAsync();   // قناة fire-and-forget محصّنة (D-69)
    }

    private async Task LoadTeachersAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<SearchTeachersHandler>().ExecuteAsync(null);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }
            Teachers.Clear();
            foreach (var teacher in result.Value!.Where(t => t.IsActive))   // الفعّالون — مرآة محرر الفوج
                Teachers.Add(teacher);
        }
        catch (Exception)
        {
            ErrorMessage = "تعذّر تحميل قائمة الأساتذة — أغلق الديالوغ وأعد فتحه.";
        }
    }

    private async Task SaveAsync()
    {
        if (SelectedTeacher is null) return;

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CorrectSessionTeacherHandler>()
                .ExecuteAsync(new CorrectSessionTeacherRequest(_sessionId, SelectedTeacher.Id));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess("صُحّحت اللقطة ✔ — أعد حساب المسودة 🔁 في شاشة الأجور لتدخل الحصة الأجور");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // D-22: المتوقع داخل الديالوغ
        }
        catch (Exception)
        {
            _notifier.ShowError("تعذّر تصحيح اللقطة — أعد المحاولة.");
        }
        finally
        {
            IsSaving = false;
        }
    }
}
