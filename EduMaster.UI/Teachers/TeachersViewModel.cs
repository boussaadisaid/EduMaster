using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Teachers;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Teachers;

public sealed class TeachersViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _searchCts;

    public TeachersViewModel(
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
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedTeacher is not null);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => SelectedTeacher is { IsActive: true });
        ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => SelectedTeacher is { IsActive: false });
        RemoveFileCommand = new AsyncRelayCommand(RemoveFileAsync, () => SelectedTeacher is not null);
    }

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

    public ObservableCollection<TeacherListItem> Teachers { get; } = new();

    private TeacherListItem? _selectedTeacher;
    public TeacherListItem? SelectedTeacher
    {
        get => _selectedTeacher;
        set
        {
            SetProperty(ref _selectedTeacher, value);
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

    public bool IsEmpty => !IsLoading && Teachers.Count == 0;

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
            var handler = scope.ServiceProvider.GetRequiredService<SearchTeachersHandler>();
            var result = await handler.ExecuteAsync(SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                Teachers.Clear();
                foreach (var teacher in result.Value!)
                    Teachers.Add(teacher);

                SelectedTeacher = SelectedTeacher is null ? null : Teachers.FirstOrDefault(t => t.Id == SelectedTeacher.Id);
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

    private async Task AddAsync()
    {
        var editor = _services.GetRequiredService<TeacherEditorViewModel>();
        editor.InitializeForCreate();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedTeacher is null) return;

        var editor = _services.GetRequiredService<TeacherEditorViewModel>();
        editor.InitializeForEdit(SelectedTeacher);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task DeactivateAsync()
    {
        var teacher = SelectedTeacher;
        if (teacher is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "تعطيل الأستاذ",
            $"سيُعطَّل «{teacher.FullName}» (تعطيل الشخص — ح-6) فيُخفى من قوائم الاختيار دون حذف شيء. يمكن إعادة تفعيله في أي وقت.",
            "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivatePersonHandler>();
        var result = await handler.ExecuteAsync(new DeactivatePersonRequest(teacher.PersonId));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّل «{teacher.FullName}»");
    }

    private async Task ActivateAsync()
    {
        var teacher = SelectedTeacher;
        if (teacher is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivatePersonHandler>();
        var result = await handler.ExecuteAsync(new ActivatePersonRequest(teacher.PersonId));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّل «{teacher.FullName}»");
    }

    private async Task RemoveFileAsync()
    {
        var teacher = SelectedTeacher;
        if (teacher is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "إزالة ملف الأستاذ",
            $"سيُزال ملف الأستاذ لـ«{teacher.FullName}» (حذف منطقي). الشخص نفسه يبقى في السجل المدني سليماً بكل بياناته.",
            "إزالة الملف");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SoftDeleteTeacherHandler>();
        var result = await handler.ExecuteAsync(new SoftDeleteTeacherRequest(teacher.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُزيل ملف الأستاذ ✔");
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