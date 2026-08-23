using EduMaster.Application.AcademicYears;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.ClassGroups;

public sealed class ClassGroupsViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _searchCts;

    public ClassGroupsViewModel(
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
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedGroup is not null);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => SelectedGroup is { IsActive: true });
        ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => SelectedGroup is { IsActive: false });
        OpenRosterCommand = new AsyncRelayCommand(OpenRosterAsync, () => SelectedGroup is not null);
    }

    // ---------- فلتر السنة ----------
    public sealed record YearFilterOption(int? Id, string Label, bool IsCurrent);

    public ObservableCollection<YearFilterOption> YearFilters { get; } = new();

    private YearFilterOption? _selectedYearFilter;
    public YearFilterOption? SelectedYearFilter
    {
        get => _selectedYearFilter;
        set
        {
            if (SetProperty(ref _selectedYearFilter, value))
            {
                _searchCts?.Cancel();   // إلغاء أي تحميل جارٍ — تبديل الفلتر فوري بلا مهلة (D-64)
                var cts = _searchCts = new CancellationTokenSource();
                _ = LoadAsync(cts.Token);
            }
        }
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
    public ObservableCollection<ClassGroupListItem> Groups { get; } = new();

    private ClassGroupListItem? _selectedGroup;
    public ClassGroupListItem? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            SetProperty(ref _selectedGroup, value);
            EditCommand.RaiseCanExecuteChanged();
            DeactivateCommand.RaiseCanExecuteChanged();
            ActivateCommand.RaiseCanExecuteChanged();
            OpenRosterCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Groups.Count == 0;

    // ---------- الأوامر ----------
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand DeactivateCommand { get; }
    public AsyncRelayCommand ActivateCommand { get; }
    public AsyncRelayCommand OpenRosterCommand { get; }

    public async Task InitializeAsync()
    {
        await LoadYearFiltersAsync();

        // الافتراضي: السنة الحالية (D-58) — والتعيين يطلق التحميل تلقائياً
        SelectedYearFilter = YearFilters.FirstOrDefault(y => y.IsCurrent) ?? YearFilters.FirstOrDefault();
    }

    private async Task LoadYearFiltersAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>();
        var result = await handler.ExecuteAsync();
        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            return;
        }

        YearFilters.Clear();
        YearFilters.Add(new YearFilterOption(null, "كل السنوات", false));
        foreach (var year in result.Value!)
            YearFilters.Add(new YearFilterOption(year.Id, year.Name.ToString(), year.IsCurrent));   // D-63: لا ToString للكيان
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetClassGroupsHandler>();
            var result = await handler.ExecuteAsync(SelectedYearFilter?.Id, SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                Groups.Clear();
                foreach (var group in result.Value!)
                    Groups.Add(group);

                SelectedGroup = SelectedGroup is null ? null : Groups.FirstOrDefault(g => g.Id == SelectedGroup.Id);
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException)
        {
            // D-64: إلغاء طلب سابق أثناء الكتابة أو تبديل الفلتر — يُبتلع بصمت
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ---------- العمليات ----------
    private async Task AddAsync()
    {
        var editor = _services.GetRequiredService<ClassGroupEditorViewModel>();
        await editor.InitializeForCreateAsync(SelectedYearFilter?.Id);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedGroup is null) return;

        var editor = _services.GetRequiredService<ClassGroupEditorViewModel>();
        await editor.InitializeForEditAsync(SelectedGroup);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    // D-75: ديالوغ المسجَّلون — أي تغيير فيه يُعيد تحميل الشبكة (عداد المسجَّلين D-80)
    private async Task OpenRosterAsync()
    {
        if (SelectedGroup is null) return;

        var dialog = _services.GetRequiredService<ClassGroupRosterDialogViewModel>();
        await dialog.InitializeAsync(SelectedGroup);

        if (await _dialogs.ShowDialogAsync(dialog, dialog.Title))
            await LoadAsync();
    }

    private async Task DeactivateAsync()
    {
        var group = SelectedGroup;
        if (group is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "تعطيل الفوج",
            $"سيُعطَّل الفوج «{group.Name}» فيتوقف عن استقبال تسجيلات جديدة دون حذف شيء — وتُلغى حصصه المستقبلية المجدولة تلقائياً (D-90). يمكن إعادة تفعيله في أي وقت.",
            "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateClassGroupHandler>();
        var result = await handler.ExecuteAsync(new DeactivateClassGroupRequest(group.Id));

        if (result.IsSuccess)
        {
            // D-90: يُذكر عدد الحصص الملغاة تلقائياً
            _notifier.ShowSuccess(result.Value > 0
                ? $"عُطّل الفوج «{group.Name}» — وأُلغيت {result.Value} حصة مستقبلية مجدولة"
                : $"عُطّل الفوج «{group.Name}»");
            await LoadAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);   // D-55: فوج فيه مسجَّلون نشطون يُرفَض هنا
    }

    private async Task ActivateAsync()
    {
        var group = SelectedGroup;
        if (group is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivateClassGroupHandler>();
        var result = await handler.ExecuteAsync(new ActivateClassGroupRequest(group.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّل الفوج «{group.Name}»");
    }

    // D-22/D-29: نجاح ← Toast · غير متوقع ← Toast خطأ · قاعدة عمل في قائمة ← Toast تحذيري
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