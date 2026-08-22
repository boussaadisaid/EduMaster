using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Teachers;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.ClassGroups;

public sealed class ClassGroupEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int? _editingGroupId;              // null = إنشاء
    private HashSet<int> _initialStreamIds = new();
    private bool _nameTouched;                 // كتابة يدوية توقف الاقتراح التلقائي (D-58)
    private bool _suppressNameTracking;

    public ClassGroupEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    public string Title => _editingGroupId is null ? "فوج جديد" : "تعديل الفوج";
    public bool IsCreateMode => _editingGroupId is null;

    // ---------- خيارات القوائم ----------
    public sealed record YearOption(int Id, string Label, bool IsActive, bool IsCurrent);
    public sealed record NamedOption(int Id, string Name);
    public sealed record OptionalTeacherOption(int? Id, string Label);
    public sealed record OptionalRoomOption(int? Id, string Label);

    public sealed class StreamOption : BaseViewModel
    {
        public StreamOption(int id, string label, bool isSelected)
        {
            Id = id;
            Label = label;
            _isSelected = isSelected;
        }

        public int Id { get; }
        public string Label { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public ObservableCollection<YearOption> Years { get; } = new();
    public ObservableCollection<NamedOption> Levels { get; } = new();
    public ObservableCollection<NamedOption> Subjects { get; } = new();
    public ObservableCollection<OptionalTeacherOption> TeacherOptions { get; } = new();
    public ObservableCollection<OptionalRoomOption> RoomOptions { get; } = new();
    public ObservableCollection<StreamOption> StreamOptions { get; } = new();

    private YearOption? _selectedYear;
    public YearOption? SelectedYear
    {
        get => _selectedYear;
        set => SetProperty(ref _selectedYear, value);
    }

    private NamedOption? _selectedLevel;
    public NamedOption? SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (SetProperty(ref _selectedLevel, value))
            {
                OnPropertyChanged(nameof(ShowStreamsSection));
                OnPropertyChanged(nameof(ShowNoStreamsMessage));
                _ = LoadStreamsForLevelAsync(value?.Id);
                SuggestNameIfUntouched();
            }
        }
    }

    private NamedOption? _selectedSubject;
    public NamedOption? SelectedSubject
    {
        get => _selectedSubject;
        set
        {
            if (SetProperty(ref _selectedSubject, value))
                SuggestNameIfUntouched();
        }
    }

    private OptionalTeacherOption? _selectedTeacher;
    public OptionalTeacherOption? SelectedTeacher
    {
        get => _selectedTeacher;
        set => SetProperty(ref _selectedTeacher, value);
    }

    private OptionalRoomOption? _selectedRoom;
    public OptionalRoomOption? SelectedRoom
    {
        get => _selectedRoom;
        set => SetProperty(ref _selectedRoom, value);
    }

    // ---------- الحقول ----------
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);
            if (!_suppressNameTracking)
                _nameTouched = true;
        }
    }

    private string _capacityText = string.Empty;
    public string CapacityText
    {
        get => _capacityText;
        set => SetProperty(ref _capacityText, value);
    }

    // ---------- الشعب ----------
    public bool ShowStreamsSection => SelectedLevel is not null;
    public bool HasStreamOptions => StreamOptions.Count > 0;
    public bool ShowNoStreamsMessage => SelectedLevel is not null && StreamOptions.Count == 0;

    // ---------- الخطأ والحفظ ----------
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

    // ---------- التهيئة ----------
    public async Task InitializeForCreateAsync(int? preferredYearId)
    {
        _editingGroupId = null;
        OnPropertyChanged(nameof(IsCreateMode));

        await LoadOptionsAsync();

        SelectedYear = Years.FirstOrDefault(y => preferredYearId is not null && y.Id == preferredYearId)
            ?? Years.FirstOrDefault(y => y.IsCurrent)
            ?? Years.FirstOrDefault(y => y.IsActive)
            ?? Years.FirstOrDefault();
        SelectedTeacher = TeacherOptions.FirstOrDefault();
        SelectedRoom = RoomOptions.FirstOrDefault();
    }

    public async Task InitializeForEditAsync(ClassGroupListItem item)
    {
        _editingGroupId = item.Id;
        _nameTouched = true;   // التحرير: لا اقتراح تلقائي
        OnPropertyChanged(nameof(IsCreateMode));

        SetNameSilently(item.Name);
        CapacityText = item.Capacity?.ToString() ?? string.Empty;

        await LoadOptionsAsync();

        // الهوية (سنة/مستوى/مادة) تُعرض معطّلة — والقيمة الحالية تُضمَّن حتى لو لم تعد فعّالة
        SelectedYear = Years.FirstOrDefault(y => y.Id == item.AcademicYearId);
        EnsureCurrentOption(Levels, item.LevelId, item.LevelName);
        EnsureCurrentOption(Subjects, item.SubjectId, item.SubjectName);
        if (item.TeacherId is not null && TeacherOptions.All(t => t.Id != item.TeacherId) && item.TeacherFullName is not null)
            TeacherOptions.Add(new OptionalTeacherOption(item.TeacherId, item.TeacherFullName + " (لم يعد فعّالاً)"));
        if (item.RoomId is not null && RoomOptions.All(r => r.Id != item.RoomId) && item.RoomName is not null)
            RoomOptions.Add(new OptionalRoomOption(item.RoomId, item.RoomName + " (لم تعد فعّالة)"));

        SelectedSubject = Subjects.FirstOrDefault(s => s.Id == item.SubjectId);
        SelectedTeacher = TeacherOptions.FirstOrDefault(t => t.Id == item.TeacherId) ?? TeacherOptions.FirstOrDefault();
        SelectedRoom = RoomOptions.FirstOrDefault(r => r.Id == item.RoomId) ?? RoomOptions.FirstOrDefault();

        // معرفات الشعب المحفوظة أولاً — ثم تعيين المستوى يبني الخيارات ويؤشّرها
        await LoadInitialStreamIdsAsync(item.Id);
        SelectedLevel = Levels.FirstOrDefault(l => l.Id == item.LevelId);
    }

    private static void EnsureCurrentOption(ObservableCollection<NamedOption> options, int id, string name)
    {
        if (options.All(o => o.Id != id))
            options.Add(new NamedOption(id, name + " (معطّل)"));
    }

    private async Task LoadOptionsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // السنوات — الكل (المعطّلة موسومة، والإنشاء عليها ممنوع واجهةً وHandlerً)
        var yearsResult = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
        if (!yearsResult.IsSuccess)
        {
            _notifier.ShowError(yearsResult.ErrorMessage!);
            return;
        }
        Years.Clear();
        foreach (var year in yearsResult.Value!)
            Years.Add(new YearOption(year.Id, year.Name + (year.IsActive ? string.Empty : " (معطّلة)"), year.IsActive, year.IsCurrent));   // التسمية من خاصية موسومة — لا ToString للكيان

        // المستويات الفعّالة فقط
        var levelsResult = await scope.ServiceProvider.GetRequiredService<GetLevelsHandler>().ExecuteAsync();
        if (!levelsResult.IsSuccess)
        {
            _notifier.ShowError(levelsResult.ErrorMessage!);
            return;
        }
        Levels.Clear();
        foreach (var level in levelsResult.Value!.Where(l => l.IsActive))
            Levels.Add(new NamedOption(level.Id, level.Name));

        // المواد الفعّالة فقط
        var subjectsResult = await scope.ServiceProvider.GetRequiredService<GetSubjectsHandler>().ExecuteAsync();
        if (!subjectsResult.IsSuccess)
        {
            _notifier.ShowError(subjectsResult.ErrorMessage!);
            return;
        }
        Subjects.Clear();
        foreach (var subject in subjectsResult.Value!.Where(s => s.IsActive))
            Subjects.Add(new NamedOption(subject.Id, subject.Name));

        // الأساتذة الفعّالون — بحث فارغ يعيد الكل
        var teachersResult = await scope.ServiceProvider.GetRequiredService<SearchTeachersHandler>().ExecuteAsync(null);
        if (!teachersResult.IsSuccess)
        {
            _notifier.ShowError(teachersResult.ErrorMessage!);
            return;
        }
        TeacherOptions.Clear();
        TeacherOptions.Add(new OptionalTeacherOption(null, "— بلا أستاذ بعد —"));
        foreach (var teacher in teachersResult.Value!.Where(t => t.IsActive))
            TeacherOptions.Add(new OptionalTeacherOption(teacher.Id, string.Join(" ",
                new[] { teacher.FirstName, teacher.LastName, teacher.FatherName }
                    .Where(p => !string.IsNullOrWhiteSpace(p)))));   // الاسم ← اللقب ← اسم الأب (D-41)

        // القاعات الفعّالة فقط — اختيارية دائماً (D-44)
        var roomsResult = await scope.ServiceProvider.GetRequiredService<GetRoomsHandler>().ExecuteAsync();
        if (!roomsResult.IsSuccess)
        {
            _notifier.ShowError(roomsResult.ErrorMessage!);
            return;
        }
        RoomOptions.Clear();
        RoomOptions.Add(new OptionalRoomOption(null, "— بلا قاعة —"));
        foreach (var room in roomsResult.Value!.Where(r => r.IsActive))
            RoomOptions.Add(new OptionalRoomOption(room.Id, room.Name));
    }

    private async Task LoadInitialStreamIdsAsync(int groupId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetClassGroupStreamIdsHandler>();
        var result = await handler.ExecuteAsync(groupId);
        if (result.IsSuccess)
            _initialStreamIds = result.Value!.ToHashSet();
        else
            _notifier.ShowError(result.ErrorMessage!);
    }

    private async Task LoadStreamsForLevelAsync(int? levelId)
    {
        StreamOptions.Clear();
        OnPropertyChanged(nameof(HasStreamOptions));
        OnPropertyChanged(nameof(ShowNoStreamsMessage));
        if (levelId is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetStreamsByLevelHandler>();
        var result = await handler.ExecuteAsync(levelId.Value);
        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            return;
        }

        foreach (var stream in result.Value!)
        {
            var isSaved = _initialStreamIds.Contains(stream.Id);
            if (!stream.IsActive && !isSaved) continue;   // المعطّلة لا تُعرض إلا إن كانت محفوظة على الفوج

            var label = stream.IsActive ? stream.Name : stream.Name + " (معطّلة)";
            StreamOptions.Add(new StreamOption(stream.Id, label, isSaved));
        }

        OnPropertyChanged(nameof(HasStreamOptions));
        OnPropertyChanged(nameof(ShowNoStreamsMessage));
    }

    // اقتراح الاسم التلقائي (D-58): مادة — مستوى، حتى أول كتابة يدوية
    private void SuggestNameIfUntouched()
    {
        if (_editingGroupId is not null || _nameTouched)
            return;
        if (SelectedSubject is null || SelectedLevel is null)
            return;

        SetNameSilently($"{SelectedSubject.Name} — {SelectedLevel.Name}");
    }

    private void SetNameSilently(string name)
    {
        _suppressNameTracking = true;
        Name = name;
        _suppressNameTracking = false;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (IsCreateMode && SelectedYear is null)
        {
            ErrorMessage = "اختر السنة الدراسية.";
            return;
        }
        if (IsCreateMode && SelectedYear is { IsActive: false })
        {
            ErrorMessage = "السنة المحددة معطّلة — لا يمكن إنشاء فوج فيها.";
            return;
        }
        if (SelectedLevel is null)
        {
            ErrorMessage = "اختر المستوى.";
            return;
        }
        if (SelectedSubject is null)
        {
            ErrorMessage = "اختر المادة.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "أدخل اسم الفوج.";
            return;
        }

        int? capacity = null;
        if (!string.IsNullOrWhiteSpace(CapacityText))
        {
            if (!int.TryParse(CapacityText.Trim(), out var parsed) || parsed <= 0)
            {
                ErrorMessage = "سعة الفوج يجب أن تكون رقماً أكبر من صفر أو تُترك فارغة.";
                return;
            }
            capacity = parsed;
        }

        var streamIds = StreamOptions.Where(o => o.IsSelected).Select(o => o.Id).ToList();

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingGroupId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateClassGroupHandler>();
                var result = await handler.ExecuteAsync(new CreateClassGroupRequest(
                    SelectedYear!.Id, SelectedLevel.Id, SelectedSubject.Id,
                    SelectedTeacher?.Id, SelectedRoom?.Id, Name, capacity, streamIds));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُنشئ الفوج بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateClassGroupHandler>();
                var result = await handler.ExecuteAsync(new UpdateClassGroupRequest(
                    _editingGroupId.Value, SelectedTeacher?.Id, SelectedRoom?.Id, Name, capacity, streamIds));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت بيانات الفوج ✔"))
                    return;
            }

            CloseRequested?.Invoke(this, true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // D-22 داخل الديالوغ: المتوقع ← بانر أحمر · غير المتوقع ← Toast
    private bool HandleSaveResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            return true;
        }

        if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            ErrorMessage = errorMessage;

        return false;
    }
}