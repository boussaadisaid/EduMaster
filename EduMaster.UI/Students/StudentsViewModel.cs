using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Students;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
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
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Students.Count == 0;

    // ---------- الأوامر ----------
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand DeactivateCommand { get; }
    public AsyncRelayCommand ActivateCommand { get; }
    public AsyncRelayCommand RemoveFileCommand { get; }

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

        // ح-7: الإزالة حذف منطقي لتصحيح إنشاء خاطئ — الشخص يبقى سليماً في السجل المدني
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