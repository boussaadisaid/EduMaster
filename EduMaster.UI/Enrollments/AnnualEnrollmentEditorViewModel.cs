using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Students;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Enrollments;

public sealed class AnnualEnrollmentEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<AnnualEnrollmentEditorViewModel> _logger;

    private int? _editingEnrollmentId;   // null = إنشاء
    private int _studentId;
    private int? _initialStreamId;       // الشعبة المحفوظة — تُؤشَّر بعد وصول قائمة الشعب
    private bool _feeTouched;            // لمس المستخدم الحقوق يدوياً ← يوقف الاقتراح (D-71)
    private bool _suppressFeeTracking;

    public AnnualEnrollmentEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<AnnualEnrollmentEditorViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _editingEnrollmentId is null ? "تسجيل سنوي جديد" : "تعديل التسجيل السنوي";
    public bool IsCreateMode => _editingEnrollmentId is null;

    private string _studentName = string.Empty;
    public string StudentName
    {
        get => _studentName;
        private set => SetProperty(ref _studentName, value);
    }

    // ---------- خيارات القوائم ----------
    // ملاحظة بنيوية: GetAllAcademicYearsHandler يعيد نموذج قراءة مسطّحاً (AcademicYearListItem) لا كياناً —
    // لذلك حقوق السنة تُجلب من الكيان عند الحاجة فقط (انظر LoadSuggestedFeeAsync)
    public sealed record YearOption(int Id, string Label, bool IsCurrent, bool IsActive);
    public sealed record NamedOption(int Id, string Name);
    public sealed record OptionalStreamOption(int? Id, string Name);

    public ObservableCollection<YearOption> Years { get; } = new();
    public ObservableCollection<NamedOption> Levels { get; } = new();
    public ObservableCollection<OptionalStreamOption> Streams { get; } = new();

    private YearOption? _selectedYear;
    public YearOption? SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
                _ = LoadSuggestedFeeAsync(value);   // D-71: اقتراح حقوق السنة المختارة
        }
    }

    private NamedOption? _selectedLevel;
    public NamedOption? SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (SetProperty(ref _selectedLevel, value))
                _ = LoadStreamsForLevelAsync(value?.Id);
        }
    }

    private OptionalStreamOption? _selectedStream;
    public OptionalStreamOption? SelectedStream
    {
        get => _selectedStream;
        set => SetProperty(ref _selectedStream, value);
    }

    // ---------- الحقوق بالدينار (D-51/D-66) ----------
    private string _feeText = string.Empty;
    public string FeeText
    {
        get => _feeText;
        set
        {
            SetProperty(ref _feeText, value);
            if (!_suppressFeeTracking)
                _feeTouched = true;
        }
    }

    private string _feeNote = string.Empty;
    public string FeeNote
    {
        get => _feeNote;
        set => SetProperty(ref _feeNote, value);
    }

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
    /// <summary>preferredYearId: سنة مسبوقة الاختيار — التدفق السريع من ديالوغ المسجَّلين يمرّر سنة الفوج (D-76)</summary>
    public async Task InitializeForCreateAsync(StudentListItem student, int? preferredYearId = null)
    {
        _editingEnrollmentId = null;
        _studentId = student.Id;
        _initialStreamId = null;
        StudentName = student.FullName;
        OnPropertyChanged(nameof(IsCreateMode));

        await LoadOptionsAsync();

        // D-71: الافتراضي السنة المفضَّلة ثم الحالية — وتعيينها يطلق جلب الاقتراح تلقائياً
        SelectedYear = Years.FirstOrDefault(y => preferredYearId is not null && y.Id == preferredYearId)
            ?? Years.FirstOrDefault(y => y.IsCurrent)
            ?? Years.FirstOrDefault(y => y.IsActive)
            ?? Years.FirstOrDefault();
    }

    public async Task InitializeForEditAsync(AnnualEnrollmentListItem item, string studentName)
    {
        _editingEnrollmentId = item.Id;
        _feeTouched = true;   // التحرير: لا اقتراح تلقائي — القيمة المحفوظة فوق أي اقتراح
        _initialStreamId = item.StreamId;
        StudentName = studentName;
        OnPropertyChanged(nameof(IsCreateMode));

        SetFeeSilently(MoneyInput.FormatDinars(item.AgreedRegistrationFeeCentimes));
        FeeNote = item.RegistrationFeeNote ?? string.Empty;

        await LoadOptionsAsync();

        // السنة ثابتة في التحرير (D-72) — تُعرض معطّلة
        SelectedYear = Years.FirstOrDefault(y => y.Id == item.AcademicYearId);
        EnsureCurrentOption(Levels, item.LevelId, item.LevelName);
        SelectedLevel = Levels.FirstOrDefault(l => l.Id == item.LevelId);   // يجرّ تحميل الشعب وتُؤشَّر المحفوظة
    }

    private static void EnsureCurrentOption(ObservableCollection<NamedOption> options, int id, string name)
    {
        if (options.All(o => o.Id != id))
            options.Add(new NamedOption(id, name + " (معطّل)"));
    }

    private async Task LoadOptionsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // السنوات — الكل (تحرير تسجيل سنة أُعطّلت لاحقاً يجب أن يعرضها) — والمسطّح بلا حقوق، فتُجلب عند الاقتراح فقط
        var yearsResult = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
        if (!yearsResult.IsSuccess)
        {
            _notifier.ShowError(yearsResult.ErrorMessage!);
            return;
        }
        Years.Clear();
        foreach (var year in yearsResult.Value!)
            Years.Add(new YearOption(year.Id, year.Name + (year.IsActive ? string.Empty : " (معطّلة)"),
                year.IsCurrent, year.IsActive));   // D-63: التسمية من خاصية موسومة

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
    }

    private async Task LoadStreamsForLevelAsync(int? levelId)
    {
        Streams.Clear();
        if (levelId is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetStreamsByLevelHandler>();
        var result = await handler.ExecuteAsync(levelId.Value);
        if (!result.IsSuccess)
        {
            _notifier.ShowError(result.ErrorMessage!);
            return;
        }

        Streams.Add(new OptionalStreamOption(null, "— بلا شعبة —"));
        foreach (var stream in result.Value!)
        {
            var isSaved = _initialStreamId == stream.Id;
            if (!stream.IsActive && !isSaved) continue;   // المعطّلة لا تُعرض إلا إن كانت محفوظة على التسجيل

            Streams.Add(new OptionalStreamOption(stream.Id, stream.IsActive ? stream.Name : stream.Name + " (معطّلة)"));
        }

        SelectedStream = Streams.FirstOrDefault(s => s.Id == _initialStreamId) ?? Streams.FirstOrDefault();
    }

    // اقتراح الحقوق من السنة المختارة (D-66/D-71) — من الكيان عبر GetAcademicYearByIdHandler، وحتى أول لمس يدوي
    private async Task LoadSuggestedFeeAsync(YearOption? year)
    {
        if (_editingEnrollmentId is not null || _feeTouched || year is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetAcademicYearByIdHandler>();
            var result = await handler.ExecuteAsync(year.Id);

            // حارسا السباق: السنة ما تزال مختارة + المستخدم لم يلمس الحقوق أثناء الجلب
            if (result.IsSuccess && result.Value is not null && SelectedYear?.Id == year.Id && !_feeTouched)
                SetFeeSilently(MoneyInput.FormatDinars(result.Value.RegistrationFeeCentimes));
            else if (!result.IsSuccess)
                _notifier.ShowError(result.ErrorMessage!);
        }
        catch (Exception ex)
        {
            // قناة fire-and-forget محصّنة (D-69): تُسجَّل وتُعلِم — لا استثناء غير مُلاحَظ
            _logger.LogError(ex, "Failed to load suggested registration fee for year {AcademicYearId}", year.Id);
            _notifier.ShowError("تعذّر اقتراح حقوق التسجيل للسنة المختارة — أدخلها يدوياً.");
        }
    }

    private void SetFeeSilently(string text)
    {
        _suppressFeeTracking = true;
        FeeText = text;
        _suppressFeeTracking = false;
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
            ErrorMessage = "لا يمكن التسجيل في سنة معطّلة.";
            return;
        }
        if (SelectedLevel is null)
        {
            ErrorMessage = "اختر المستوى.";
            return;
        }
        if (!MoneyInput.TryParseDinars(FeeText, out var feeCentimes))
        {
            ErrorMessage = "حقوق التسجيل يجب أن تكون مبلغاً صحيحاً بالدينار (مثل 0 أو 1500) — والفارغ = صفر (إعفاء).";
            return;
        }

        var feeNote = string.IsNullOrWhiteSpace(FeeNote) ? null : FeeNote;

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingEnrollmentId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<RegisterAnnualEnrollmentHandler>();
                var result = await handler.ExecuteAsync(new RegisterAnnualEnrollmentRequest(
                    _studentId, SelectedYear!.Id, SelectedLevel.Id, SelectedStream?.Id, feeCentimes, feeNote));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "سُجّل الطالب في السنة بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateAnnualEnrollmentHandler>();
                var result = await handler.ExecuteAsync(new UpdateAnnualEnrollmentRequest(
                    _editingEnrollmentId.Value, SelectedLevel.Id, SelectedStream?.Id, feeCentimes, feeNote));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظ التسجيل ✔"))
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