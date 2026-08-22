using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.Common;
using EduMaster.Application.Pricing;
using EduMaster.Domain.Academic;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Pricing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Stream = EduMaster.Domain.Academic.Stream;   // احتياط ضد الغموض مع System.IO.Stream

namespace EduMaster.UI.Academic;

public sealed class AcademicStructureViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _priceLoadCts;

    public AcademicStructureViewModel(
        IServiceScopeFactory scopeFactory,
        IServiceProvider services,
        IUserNotifier notifier,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        // مستويات
        AddLevelCommand = new AsyncRelayCommand(AddLevelAsync);
        EditLevelCommand = new AsyncRelayCommand(EditLevelAsync, () => SelectedLevel is not null);
        DeactivateLevelCommand = new AsyncRelayCommand(DeactivateLevelAsync, () => SelectedLevel is { IsActive: true });
        ActivateLevelCommand = new AsyncRelayCommand(ActivateLevelAsync, () => SelectedLevel is { IsActive: false });

        // شعب — الإضافة تشترط مستوىً فعّالاً محدداً
        AddStreamCommand = new AsyncRelayCommand(AddStreamAsync, () => SelectedLevel is { IsActive: true });
        EditStreamCommand = new AsyncRelayCommand(EditStreamAsync, () => SelectedStream is not null);
        DeactivateStreamCommand = new AsyncRelayCommand(DeactivateStreamAsync, () => SelectedStream is { IsActive: true });
        ActivateStreamCommand = new AsyncRelayCommand(ActivateStreamAsync, () => SelectedStream is { IsActive: false });

        // مواد
        AddSubjectCommand = new AsyncRelayCommand(AddSubjectAsync);
        EditSubjectCommand = new AsyncRelayCommand(EditSubjectAsync, () => SelectedSubject is not null);
        DeactivateSubjectCommand = new AsyncRelayCommand(DeactivateSubjectAsync, () => SelectedSubject is { IsActive: true });
        ActivateSubjectCommand = new AsyncRelayCommand(ActivateSubjectAsync, () => SelectedSubject is { IsActive: false });

        // قاعات
        AddRoomCommand = new AsyncRelayCommand(AddRoomAsync);
        EditRoomCommand = new AsyncRelayCommand(EditRoomAsync, () => SelectedRoom is not null);
        DeactivateRoomCommand = new AsyncRelayCommand(DeactivateRoomAsync, () => SelectedRoom is { IsActive: true });
        ActivateRoomCommand = new AsyncRelayCommand(ActivateRoomAsync, () => SelectedRoom is { IsActive: false });

        // أسعار (F2 — الشريحة 2.2)
        AddPriceCommand = new AsyncRelayCommand(AddPriceAsync);
        EditPriceCommand = new AsyncRelayCommand(EditPriceAsync, () => SelectedPrice is not null);
        DeletePriceCommand = new AsyncRelayCommand(DeletePriceAsync, () => SelectedPrice is not null);
    }

    // ---------- الحالة ----------
    public ObservableCollection<Level> Levels { get; } = new();
    public ObservableCollection<Stream> Streams { get; } = new();
    public ObservableCollection<Subject> Subjects { get; } = new();
    public ObservableCollection<Room> Rooms { get; } = new();

    private Level? _selectedLevel;
    public Level? SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            SetProperty(ref _selectedLevel, value);
            RaiseLevelCommandsCanExecute();
            RaiseStreamCommandsCanExecute();
            _ = LoadStreamsAsync();   // Master-Detail: المستوى المحدد ← شعبه
        }
    }

    private Stream? _selectedStream;
    public Stream? SelectedStream
    {
        get => _selectedStream;
        set { SetProperty(ref _selectedStream, value); RaiseStreamCommandsCanExecute(); }
    }

    private Subject? _selectedSubject;
    public Subject? SelectedSubject
    {
        get => _selectedSubject;
        set { SetProperty(ref _selectedSubject, value); RaiseSubjectCommandsCanExecute(); }
    }

    private Room? _selectedRoom;
    public Room? SelectedRoom
    {
        get => _selectedRoom;
        set { SetProperty(ref _selectedRoom, value); RaiseRoomCommandsCanExecute(); }
    }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    // ---------- الأسعار: فلتر السنة + القائمة (D-67) ----------
    public sealed record PriceYearFilterOption(int? Id, string Label, bool IsCurrent);

    public ObservableCollection<PriceYearFilterOption> PriceYearFilters { get; } = new();

    private PriceYearFilterOption? _selectedPriceYearFilter;
    public PriceYearFilterOption? SelectedPriceYearFilter
    {
        get => _selectedPriceYearFilter;
        set
        {
            if (SetProperty(ref _selectedPriceYearFilter, value))
            {
                _priceLoadCts?.Cancel();   // D-64: تبديل الفلتر يلغي التحميل السابق
                var cts = _priceLoadCts = new CancellationTokenSource();
                _ = LoadPricesAsync(cts.Token);
            }
        }
    }

    public ObservableCollection<SubjectPriceListItem> Prices { get; } = new();

    private SubjectPriceListItem? _selectedPrice;
    public SubjectPriceListItem? SelectedPrice
    {
        get => _selectedPrice;
        set
        {
            SetProperty(ref _selectedPrice, value);
            EditPriceCommand.RaiseCanExecuteChanged();
            DeletePriceCommand.RaiseCanExecuteChanged();
        }
    }

    public bool PricesEmpty => Prices.Count == 0;

    // ---------- الأوامر التسعة عشر ----------
    public AsyncRelayCommand AddLevelCommand { get; }
    public AsyncRelayCommand EditLevelCommand { get; }
    public AsyncRelayCommand DeactivateLevelCommand { get; }
    public AsyncRelayCommand ActivateLevelCommand { get; }
    public AsyncRelayCommand AddStreamCommand { get; }
    public AsyncRelayCommand EditStreamCommand { get; }
    public AsyncRelayCommand DeactivateStreamCommand { get; }
    public AsyncRelayCommand ActivateStreamCommand { get; }
    public AsyncRelayCommand AddSubjectCommand { get; }
    public AsyncRelayCommand EditSubjectCommand { get; }
    public AsyncRelayCommand DeactivateSubjectCommand { get; }
    public AsyncRelayCommand ActivateSubjectCommand { get; }
    public AsyncRelayCommand AddRoomCommand { get; }
    public AsyncRelayCommand EditRoomCommand { get; }
    public AsyncRelayCommand DeactivateRoomCommand { get; }
    public AsyncRelayCommand ActivateRoomCommand { get; }
    public AsyncRelayCommand AddPriceCommand { get; }
    public AsyncRelayCommand EditPriceCommand { get; }
    public AsyncRelayCommand DeletePriceCommand { get; }

    // ---------- التحميل ----------
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await LoadLevelsAsync();      // ويجرّ الشعب تلقائياً عبر SelectedLevel
            await LoadSubjectsAsync();
            await LoadRoomsAsync();
            await LoadPriceYearsAsync();  // ويجرّ الأسعار تلقائياً عبر SelectedPriceYearFilter (السنة الحالية افتراضياً)
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLevelsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetLevelsHandler>();
        var result = await handler.ExecuteAsync();

        if (result.IsSuccess)
        {
            var keepId = SelectedLevel?.Id;
            Levels.Clear();
            foreach (var item in result.Value!) Levels.Add(item);
            SelectedLevel = keepId is null ? Levels.FirstOrDefault() : Levels.FirstOrDefault(l => l.Id == keepId);
        }
        else _notifier.ShowError(result.ErrorMessage!);
    }

    private async Task LoadStreamsAsync()
    {
        var levelId = SelectedLevel?.Id;
        if (levelId is null)
        {
            Streams.Clear();
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetStreamsByLevelHandler>();
        var result = await handler.ExecuteAsync(levelId.Value);

        if (result.IsSuccess)
        {
            var keepId = SelectedStream?.Id;
            Streams.Clear();
            foreach (var item in result.Value!) Streams.Add(item);
            SelectedStream = keepId is null ? null : Streams.FirstOrDefault(s => s.Id == keepId);
        }
        else _notifier.ShowError(result.ErrorMessage!);
    }

    private async Task LoadSubjectsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetSubjectsHandler>();
        var result = await handler.ExecuteAsync();

        if (result.IsSuccess)
        {
            var keepId = SelectedSubject?.Id;
            Subjects.Clear();
            foreach (var item in result.Value!) Subjects.Add(item);
            SelectedSubject = keepId is null ? null : Subjects.FirstOrDefault(s => s.Id == keepId);
        }
        else _notifier.ShowError(result.ErrorMessage!);
    }

    private async Task LoadRoomsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetRoomsHandler>();
        var result = await handler.ExecuteAsync();

        if (result.IsSuccess)
        {
            var keepId = SelectedRoom?.Id;
            Rooms.Clear();
            foreach (var item in result.Value!) Rooms.Add(item);
            SelectedRoom = keepId is null ? null : Rooms.FirstOrDefault(r => r.Id == keepId);
        }
        else _notifier.ShowError(result.ErrorMessage!);
    }

    // ---------- الأسعار: التحميل ----------
    private async Task LoadPriceYearsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>();
        var result = await handler.ExecuteAsync();

        if (result.IsSuccess)
        {
            PriceYearFilters.Clear();
            PriceYearFilters.Add(new PriceYearFilterOption(null, "كل السنوات", false));
            foreach (var year in result.Value!)
                PriceYearFilters.Add(new PriceYearFilterOption(year.Id, year.Name.ToString(), year.IsCurrent));   // D-63: لا ToString للكيان

            // الافتراضي: السنة الحالية (D-67) — والتعيين يطلق تحميل الأسعار تلقائياً
            SelectedPriceYearFilter = PriceYearFilters.FirstOrDefault(y => y.IsCurrent) ?? PriceYearFilters.FirstOrDefault();
        }
        else _notifier.ShowError(result.ErrorMessage!);
    }

    private async Task LoadPricesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetSubjectPricesHandler>();
            var result = await handler.ExecuteAsync(SelectedPriceYearFilter?.Id, cancellationToken);

            if (result.IsSuccess)
            {
                var keepId = SelectedPrice?.Id;
                Prices.Clear();
                foreach (var item in result.Value!) Prices.Add(item);
                SelectedPrice = keepId is null ? null : Prices.FirstOrDefault(p => p.Id == keepId);
                OnPropertyChanged(nameof(PricesEmpty));
            }
            else _notifier.ShowError(result.ErrorMessage!);
        }
        catch (OperationCanceledException) { }   // D-64: إلغاء تحميل سابق أثناء تبديل الفلتر — ليس خطأ
    }

    // ---------- مستويات ----------
    private async Task AddLevelAsync()
    {
        var editor = _services.GetRequiredService<LevelEditorViewModel>();
        editor.InitializeForCreate(Levels.Count == 0 ? 1 : Levels.Max(l => l.SortOrder) + 1);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadLevelsAsync();
    }

    private async Task EditLevelAsync()
    {
        if (SelectedLevel is null) return;

        var editor = _services.GetRequiredService<LevelEditorViewModel>();
        editor.InitializeForEdit(SelectedLevel);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadLevelsAsync();
    }

    private async Task DeactivateLevelAsync()
    {
        var level = SelectedLevel;
        if (level is null) return;

        var confirmed = await _dialogs.ConfirmAsync("تعطيل المستوى",
            $"سيُعطَّل المستوى «{level.Name}» فيُخفى من قوائم الاختيار دون حذف شيء (شعبه تبقى لكنها لا تُعرض معه). يمكن إعادة تفعيله في أي وقت.", "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateLevelHandler>();
        var result = await handler.ExecuteAsync(new DeactivateLevelRequest(level.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّل المستوى «{level.Name}»", LoadLevelsAsync);
    }

    private async Task ActivateLevelAsync()
    {
        var level = SelectedLevel;
        if (level is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivateLevelHandler>();
        var result = await handler.ExecuteAsync(new ActivateLevelRequest(level.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّل المستوى «{level.Name}»", LoadLevelsAsync);
    }

    // ---------- شعب ----------
    private async Task AddStreamAsync()
    {
        if (SelectedLevel is not { IsActive: true }) return;

        var editor = _services.GetRequiredService<StreamEditorViewModel>();
        editor.InitializeForCreate(SelectedLevel);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadStreamsAsync();
    }

    private async Task EditStreamAsync()
    {
        if (SelectedStream is null || SelectedLevel is null) return;

        var editor = _services.GetRequiredService<StreamEditorViewModel>();
        editor.InitializeForEdit(SelectedStream, SelectedLevel.Name);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadStreamsAsync();
    }

    private async Task DeactivateStreamAsync()
    {
        var stream = SelectedStream;
        if (stream is null) return;

        var confirmed = await _dialogs.ConfirmAsync("تعطيل الشعبة",
            $"ستُعطَّل الشعبة «{stream.Name}» فتُخفى من قوائم الاختيار دون حذف شيء. يمكن إعادة تفعيلها في أي وقت.", "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateStreamHandler>();
        var result = await handler.ExecuteAsync(new DeactivateStreamRequest(stream.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّلت الشعبة «{stream.Name}»", LoadStreamsAsync);
    }

    private async Task ActivateStreamAsync()
    {
        var stream = SelectedStream;
        if (stream is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivateStreamHandler>();
        var result = await handler.ExecuteAsync(new ActivateStreamRequest(stream.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّلت الشعبة «{stream.Name}»", LoadStreamsAsync);
    }

    // ---------- مواد ----------
    private async Task AddSubjectAsync()
    {
        var editor = _services.GetRequiredService<SubjectEditorViewModel>();
        editor.InitializeForCreate();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadSubjectsAsync();
    }

    private async Task EditSubjectAsync()
    {
        if (SelectedSubject is null) return;

        var editor = _services.GetRequiredService<SubjectEditorViewModel>();
        editor.InitializeForEdit(SelectedSubject);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadSubjectsAsync();
    }

    private async Task DeactivateSubjectAsync()
    {
        var subject = SelectedSubject;
        if (subject is null) return;

        var confirmed = await _dialogs.ConfirmAsync("تعطيل المادة",
            $"ستُعطَّل المادة «{subject.Name}» فتُخفى من قوائم الاختيار دون حذف شيء. يمكن إعادة تفعيلها في أي وقت.", "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateSubjectHandler>();
        var result = await handler.ExecuteAsync(new DeactivateSubjectRequest(subject.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّلت المادة «{subject.Name}»", LoadSubjectsAsync);
    }

    private async Task ActivateSubjectAsync()
    {
        var subject = SelectedSubject;
        if (subject is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivateSubjectHandler>();
        var result = await handler.ExecuteAsync(new ActivateSubjectRequest(subject.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّلت المادة «{subject.Name}»", LoadSubjectsAsync);
    }

    // ---------- قاعات ----------
    private async Task AddRoomAsync()
    {
        var editor = _services.GetRequiredService<RoomEditorViewModel>();
        editor.InitializeForCreate();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadRoomsAsync();
    }

    private async Task EditRoomAsync()
    {
        if (SelectedRoom is null) return;

        var editor = _services.GetRequiredService<RoomEditorViewModel>();
        editor.InitializeForEdit(SelectedRoom);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadRoomsAsync();
    }

    private async Task DeactivateRoomAsync()
    {
        var room = SelectedRoom;
        if (room is null) return;

        var confirmed = await _dialogs.ConfirmAsync("تعطيل القاعة",
            $"ستُعطَّل القاعة «{room.Name}» فتُخفى من قوائم الاختيار دون حذف شيء. يمكن إعادة تفعيلها في أي وقت.", "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateRoomHandler>();
        var result = await handler.ExecuteAsync(new DeactivateRoomRequest(room.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّلت القاعة «{room.Name}»", LoadRoomsAsync);
    }

    private async Task ActivateRoomAsync()
    {
        var room = SelectedRoom;
        if (room is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivateRoomHandler>();
        var result = await handler.ExecuteAsync(new ActivateRoomRequest(room.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّلت القاعة «{room.Name}»", LoadRoomsAsync);
    }

    // ---------- أسعار ----------
    private async Task AddPriceAsync()
    {
        var editor = _services.GetRequiredService<SubjectPriceEditorViewModel>();
        await editor.InitializeForCreateAsync(SelectedPriceYearFilter?.Id);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadPricesAsync();
    }

    private async Task EditPriceAsync()
    {
        if (SelectedPrice is null) return;

        var editor = _services.GetRequiredService<SubjectPriceEditorViewModel>();
        await editor.InitializeForEditAsync(SelectedPrice);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadPricesAsync();
    }

    private async Task DeletePriceAsync()
    {
        var price = SelectedPrice;
        if (price is null) return;

        // D-65: حذف فيزيائي حر — النسخ اللحظية عند التسجيل (2.4) لا تتأثر
        var confirmed = await _dialogs.ConfirmAsync(
            "حذف السعر",
            $"سيُحذف سعر «{price.SubjectName} — {price.LevelName}» لسنة {price.AcademicYearName} نهائياً من جدول الأسعار. أسعار التسجيلات المأخوذة سابقاً (النسخ) لا تتأثر.",
            "حذف نهائي");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeleteSubjectPriceHandler>();
        var result = await handler.ExecuteAsync(new DeleteSubjectPriceRequest(price.Id));
        await HandleResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُذف السعر ✔", () => LoadPricesAsync());
    }

    // ---------- مساعدات ----------
    private async Task HandleResultAsync(bool isSuccess, string? errorMessage, ErrorType errorType,
        string successMessage, Func<Task> reload)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            await reload();
        }
        else if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            _notifier.ShowWarning(errorMessage!);
    }

    private void RaiseLevelCommandsCanExecute()
    {
        EditLevelCommand.RaiseCanExecuteChanged();
        DeactivateLevelCommand.RaiseCanExecuteChanged();
        ActivateLevelCommand.RaiseCanExecuteChanged();
    }

    private void RaiseStreamCommandsCanExecute()
    {
        AddStreamCommand.RaiseCanExecuteChanged();
        EditStreamCommand.RaiseCanExecuteChanged();
        DeactivateStreamCommand.RaiseCanExecuteChanged();
        ActivateStreamCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSubjectCommandsCanExecute()
    {
        EditSubjectCommand.RaiseCanExecuteChanged();
        DeactivateSubjectCommand.RaiseCanExecuteChanged();
        ActivateSubjectCommand.RaiseCanExecuteChanged();
    }

    private void RaiseRoomCommandsCanExecute()
    {
        EditRoomCommand.RaiseCanExecuteChanged();
        DeactivateRoomCommand.RaiseCanExecuteChanged();
        ActivateRoomCommand.RaiseCanExecuteChanged();
    }
}