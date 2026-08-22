using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.People;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.UI.ClassGroups;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Enrollments;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Students;

public sealed class StudentsViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _enrollmentsCts;

    public StudentsViewModel(
        IServiceScopeFactory scopeFactory,
        IServiceProvider services,
        IUserNotifier notifier,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedStudent is not null);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => SelectedStudent is { IsActive: true });
        ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => SelectedStudent is { IsActive: false });
        RemoveFileCommand = new AsyncRelayCommand(RemoveFileAsync, () => SelectedStudent is not null);

        // التسجيلات السنوية (F2 — الشريحة 2.3 · D-70)
        AddEnrollmentCommand = new AsyncRelayCommand(AddEnrollmentAsync, () => SelectedStudent is { IsActive: true });
        EditEnrollmentCommand = new AsyncRelayCommand(EditEnrollmentAsync, () => SelectedEnrollment is { Status: EnrollmentStatus.Active });
        WithdrawEnrollmentCommand = new AsyncRelayCommand(WithdrawEnrollmentAsync, () => SelectedEnrollment is { Status: EnrollmentStatus.Active });

        // أفواجه: العمليات (F2 — الشريحة 2.4 · D-84: اللوحة لم تعد قراءةً فقط)
        EnrollInGroupCommand = new AsyncRelayCommand(EnrollInGroupAsync, () => SelectedStudent is { IsActive: true });
        WithdrawGroupEnrollmentCommand = new AsyncRelayCommand(WithdrawGroupEnrollmentAsync, () => SelectedGroupEnrollment is { Status: EnrollmentStatus.Active });
        TransferGroupEnrollmentCommand = new AsyncRelayCommand(TransferGroupEnrollmentAsync, () => SelectedGroupEnrollment is { Status: EnrollmentStatus.Active });
    }

    // ---------- البحث الفوري ----------
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            _ = DebouncedSearchAsync();
        }
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);
            await LoadAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    // ---------- الحالة ----------
    public ObservableCollection<StudentListItem> Students { get; } = new();

    private StudentListItem? _selectedStudent;
    public StudentListItem? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            SetProperty(ref _selectedStudent, value);
            EditCommand.RaiseCanExecuteChanged();
            DeactivateCommand.RaiseCanExecuteChanged();
            ActivateCommand.RaiseCanExecuteChanged();
            RemoveFileCommand.RaiseCanExecuteChanged();

            // اللوحة الجانبية (D-70/D-84): تحديد طالب ← تسجيلاته السنوية + أفواجه — مع إلغاء أي تحميل سابق (D-64)
            AddEnrollmentCommand.RaiseCanExecuteChanged();
            EnrollInGroupCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NoStudentSelected));
            OnPropertyChanged(nameof(EnrollmentsEmpty));
            OnPropertyChanged(nameof(StudentGroupsEmpty));
            _enrollmentsCts?.Cancel();
            var cts = _enrollmentsCts = new CancellationTokenSource();
            _ = LoadEnrollmentsAsync(cts.Token);
            _ = LoadStudentGroupsAsync(cts.Token);
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Students.Count == 0;

    // ---------- اللوحة الجانبية: التسجيلات السنوية ----------
    public ObservableCollection<AnnualEnrollmentListItem> Enrollments { get; } = new();

    private AnnualEnrollmentListItem? _selectedEnrollment;
    public AnnualEnrollmentListItem? SelectedEnrollment
    {
        get => _selectedEnrollment;
        set
        {
            SetProperty(ref _selectedEnrollment, value);
            EditEnrollmentCommand.RaiseCanExecuteChanged();
            WithdrawEnrollmentCommand.RaiseCanExecuteChanged();
        }
    }

    public bool NoStudentSelected => SelectedStudent is null;
    public bool EnrollmentsEmpty => SelectedStudent is not null && Enrollments.Count == 0;

    // ---------- اللوحة الجانبية: أفواجه ----------
    public ObservableCollection<StudentGroupEnrollmentItem> StudentGroups { get; } = new();

    private StudentGroupEnrollmentItem? _selectedGroupEnrollment;
    public StudentGroupEnrollmentItem? SelectedGroupEnrollment
    {
        get => _selectedGroupEnrollment;
        set
        {
            SetProperty(ref _selectedGroupEnrollment, value);
            WithdrawGroupEnrollmentCommand.RaiseCanExecuteChanged();
            TransferGroupEnrollmentCommand.RaiseCanExecuteChanged();
        }
    }

    public bool StudentGroupsEmpty => SelectedStudent is not null && StudentGroups.Count == 0;

    // ---------- الأوامر ----------
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand DeactivateCommand { get; }
    public AsyncRelayCommand ActivateCommand { get; }
    public AsyncRelayCommand RemoveFileCommand { get; }
    public AsyncRelayCommand AddEnrollmentCommand { get; }
    public AsyncRelayCommand EditEnrollmentCommand { get; }
    public AsyncRelayCommand WithdrawEnrollmentCommand { get; }
    public AsyncRelayCommand EnrollInGroupCommand { get; }
    public AsyncRelayCommand WithdrawGroupEnrollmentCommand { get; }
    public AsyncRelayCommand TransferGroupEnrollmentCommand { get; }

    public Task InitializeAsync() => LoadAsync();

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SearchStudentsHandler>();
            var result = await handler.ExecuteAsync(SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                Students.Clear();
                foreach (var student in result.Value!)
                    Students.Add(student);

                SelectedStudent = SelectedStudent is null ? null : Students.FirstOrDefault(s => s.Id == SelectedStudent.Id);
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadEnrollmentsAsync(CancellationToken cancellationToken = default)
    {
        var student = SelectedStudent;
        if (student is null)
        {
            Enrollments.Clear();
            SelectedEnrollment = null;
            OnPropertyChanged(nameof(EnrollmentsEmpty));
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetAnnualEnrollmentsForStudentHandler>();
            var result = await handler.ExecuteAsync(student.Id, cancellationToken);

            if (result.IsSuccess)
            {
                Enrollments.Clear();
                foreach (var item in result.Value!)
                    Enrollments.Add(item);
                SelectedEnrollment = null;
                OnPropertyChanged(nameof(EnrollmentsEmpty));
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException) { }   // D-64
    }

    private async Task LoadStudentGroupsAsync(CancellationToken cancellationToken = default)
    {
        var student = SelectedStudent;
        if (student is null)
        {
            StudentGroups.Clear();
            SelectedGroupEnrollment = null;
            OnPropertyChanged(nameof(StudentGroupsEmpty));
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetStudentGroupEnrollmentsHandler>();
            var result = await handler.ExecuteAsync(student.Id, cancellationToken);

            if (result.IsSuccess)
            {
                StudentGroups.Clear();
                foreach (var item in result.Value!)
                    StudentGroups.Add(item);
                SelectedGroupEnrollment = null;
                OnPropertyChanged(nameof(StudentGroupsEmpty));
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException) { }   // D-64
    }

    // ---------- العمليات ----------
    private async Task AddAsync()
    {
        var editor = _services.GetRequiredService<StudentEditorViewModel>();
        editor.InitializeForCreate();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedStudent is null) return;

        var editor = _services.GetRequiredService<StudentEditorViewModel>();
        editor.InitializeForEdit(SelectedStudent);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task DeactivateAsync()
    {
        var student = SelectedStudent;
        if (student is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "تعطيل الطالب",
            $"سيُعطَّل «{student.FullName}» (تعطيل الشخص — ح-6) فيُخفى من قوائم الاختيار دون حذف شيء. يمكن إعادة تفعيله في أي وقت.",
            "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivatePersonHandler>();
        var result = await handler.ExecuteAsync(new DeactivatePersonRequest(student.PersonId));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّل «{student.FullName}»");
    }

    private async Task ActivateAsync()
    {
        var student = SelectedStudent;
        if (student is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivatePersonHandler>();
        var result = await handler.ExecuteAsync(new ActivatePersonRequest(student.PersonId));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّل «{student.FullName}»");
    }

    private async Task RemoveFileAsync()
    {
        var student = SelectedStudent;
        if (student is null) return;

        // ح-7/D-73: الإزالة حذف منطقي — وعليه تسجيلات تُمنع برسالة عربية (الحارس مفعَّل)
        var confirmed = await _dialogs.ConfirmAsync(
            "إزالة ملف الطالب",
            $"سيُزال ملف الطالب لـ«{student.FullName}» (حذف منطقي). الشخص نفسه يبقى في السجل المدني سليماً بكل بياناته.",
            "إزالة الملف");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SoftDeleteStudentHandler>();
        var result = await handler.ExecuteAsync(new SoftDeleteStudentRequest(student.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُزيل ملف الطالب ✔");
    }

    // ---------- التسجيلات السنوية: العمليات ----------
    private async Task AddEnrollmentAsync()
    {
        var student = SelectedStudent;
        if (student is null) return;

        var editor = _services.GetRequiredService<AnnualEnrollmentEditorViewModel>();
        await editor.InitializeForCreateAsync(student);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadEnrollmentsAsync();
    }

    private async Task EditEnrollmentAsync()
    {
        var enrollment = SelectedEnrollment;
        var student = SelectedStudent;
        if (enrollment is null || student is null) return;

        var editor = _services.GetRequiredService<AnnualEnrollmentEditorViewModel>();
        await editor.InitializeForEditAsync(enrollment, student.FullName);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadEnrollmentsAsync();
    }

    private async Task WithdrawEnrollmentAsync()
    {
        var enrollment = SelectedEnrollment;
        var student = SelectedStudent;
        if (enrollment is null || student is null) return;

        // D-53: الانسحاب السنوي يسحب أفواجه النشطة معه في معاملة واحدة (مفعَّل منذ 2.4)
        var confirmed = await _dialogs.ConfirmAsync(
            "انسحاب من السنة",
            $"سيُسجَّل انسحاب «{student.FullName}» من سنة {enrollment.AcademicYearName} وتُسحب معه أفواجه النشطة إن وُجدت. تبقى البيانات محفوظة، ويمكن تسجيله من جديد في أي وقت.",
            "تأكيد الانسحاب");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<WithdrawAnnualEnrollmentHandler>();
        var result = await handler.ExecuteAsync(new WithdrawAnnualEnrollmentRequest(enrollment.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, "سُجّل الانسحاب ✔",
            async () =>
            {
                await LoadEnrollmentsAsync();
                await LoadStudentGroupsAsync();
            });
    }

    // ---------- أفواجه: العمليات (D-84 — نفس الـHandlers، مدخل ثانٍ) ----------
    private async Task EnrollInGroupAsync()
    {
        var student = SelectedStudent;
        if (student is null) return;

        var dialog = _services.GetRequiredService<EnrollInGroupDialogViewModel>();
        await dialog.InitializeAsync(student);

        if (await _dialogs.ShowDialogAsync(dialog, dialog.Title))
        {
            await LoadStudentGroupsAsync();
            await LoadEnrollmentsAsync();   // قد يكون أنشأ تسجيلاً سنوياً من التدفق السريع (D-76)
        }
    }

    private async Task WithdrawGroupEnrollmentAsync()
    {
        var enrollment = SelectedGroupEnrollment;
        var student = SelectedStudent;
        if (enrollment is null || student is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "انسحاب من الفوج",
            $"سيُسجَّل انسحاب «{student.FullName}» من فوج «{enrollment.ClassGroupName}». يبقى تسجيله السنوي نشطاً وتاريخه محفوظاً، ويمكن إعادة إلحاقه في أي وقت.",
            "تأكيد الانسحاب");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<WithdrawGroupEnrollmentHandler>();
        var result = await handler.ExecuteAsync(new WithdrawGroupEnrollmentRequest(enrollment.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType,
            $"سُجّل الانسحاب من «{enrollment.ClassGroupName}» ✔", () => LoadStudentGroupsAsync());
    }

    private async Task TransferGroupEnrollmentAsync()
    {
        var enrollment = SelectedGroupEnrollment;
        var student = SelectedStudent;
        if (enrollment is null || student is null) return;

        var dialog = _services.GetRequiredService<TransferGroupEnrollmentViewModel>();
        await dialog.InitializeAsync(enrollment.Id, student.FullName, enrollment.ClassGroupName);

        if (await _dialogs.ShowDialogAsync(dialog, dialog.Title))
            await LoadStudentGroupsAsync();
    }

    private async Task HandleResultAsync(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage,
        Func<Task>? reload = null)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            await (reload?.Invoke() ?? LoadAsync());
        }
        else if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            _notifier.ShowWarning(errorMessage!);
    }
}