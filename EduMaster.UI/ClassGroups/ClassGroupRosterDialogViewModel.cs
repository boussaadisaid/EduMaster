using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Pricing;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Enrollments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.ClassGroups;

/// <summary>
/// ديالوغ «المسجَّلون» (D-75، منحوف D-85): قائمة الفوج + إلحاق طالب ببحث حي + تدفق سريع (D-76)
/// + سعر مقترح (D-77) + حصص مبدئية في معاملة الإلحاق (D-97) + انسحاب + نقل (D-78)
/// </summary>
public sealed class ClassGroupRosterDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ClassGroupRosterDialogViewModel> _logger;

    private ClassGroupListItem _group = null!;
    private bool _changed;                          // أي عملية ناجحة ← الشاشة الأم تُعيد التحميل (عداد المسجَّلين)
    private CancellationTokenSource? _searchCts;

    public ClassGroupRosterDialogViewModel(IServiceScopeFactory scopeFactory, IServiceProvider services,
        IUserNotifier notifier, IDialogService dialogs, ILogger<ClassGroupRosterDialogViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;
        _logger = logger;

        CloseCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, _changed);
            return Task.CompletedTask;
        });
        ClearPickedCommand = new AsyncRelayCommand(() =>
        {
            PickedStudent = null;
            return Task.CompletedTask;
        });
        CreateAnnualCommand = new AsyncRelayCommand(CreateAnnualAsync, () => PickedStudent is not null && AnnualMissing);
        EnrollCommand = new AsyncRelayCommand(EnrollAsync, () => CanEnrollPicked && !IsBusy);
        WithdrawCommand = new AsyncRelayCommand(WithdrawAsync, () => SelectedEnrollment is { Status: EnrollmentStatus.Active });
        TransferCommand = new AsyncRelayCommand(TransferAsync, () => SelectedEnrollment is { Status: EnrollmentStatus.Active });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => "مسجَّلو الفوج";

    // ---------- ترويسة الفوج ----------
    public string GroupHeader => _group is null ? string.Empty
        : $"{_group.Name} — {_group.SubjectName} · {_group.LevelName} · {_group.StreamsDisplay}";

    private string _enrolledSummary = string.Empty;
    public string EnrolledSummary
    {
        get => _enrolledSummary;
        private set => SetProperty(ref _enrolledSummary, value);
    }

    // ---------- القائمة ----------
    public ObservableCollection<ClassGroupEnrollmentListItem> Roster { get; } = new();

    private ClassGroupEnrollmentListItem? _selectedEnrollment;
    public ClassGroupEnrollmentListItem? SelectedEnrollment
    {
        get => _selectedEnrollment;
        set
        {
            SetProperty(ref _selectedEnrollment, value);
            WithdrawCommand.RaiseCanExecuteChanged();
            TransferCommand.RaiseCanExecuteChanged();
        }
    }

    public bool RosterEmpty => Roster.Count == 0;

    // ---------- منتقي الطالب (بحث حي بمهلة 300ms) ----------
    private string _studentSearchText = string.Empty;
    public string StudentSearchText
    {
        get => _studentSearchText;
        set
        {
            SetProperty(ref _studentSearchText, value);
            _ = DebouncedStudentSearchAsync();
        }
    }

    public ObservableCollection<StudentListItem> StudentResults { get; } = new();

    /// <summary>D-85: قائمة النتائج تظهر فقط عند وجود نتائج وعدم الالتقاط — لا تحجز مكاناً فارغاً</summary>
    public bool ShowStudentResults => HasNoPickedStudent && StudentResults.Count > 0;

    private StudentListItem? _pickedStudent;
    public StudentListItem? PickedStudent
    {
        get => _pickedStudent;
        set
        {
            SetProperty(ref _pickedStudent, value);
            OnPropertyChanged(nameof(HasPickedStudent));
            OnPropertyChanged(nameof(HasNoPickedStudent));
            OnPropertyChanged(nameof(PickedStudentName));
            OnPropertyChanged(nameof(ShowCreateAnnual));
            OnPropertyChanged(nameof(ShowStudentResults));
            CreateAnnualCommand.RaiseCanExecuteChanged();
            _ = RefreshEligibilityAsync();   // يفحص التسجيل السنوي المطابق ويقترح السعر
        }
    }

    public bool HasPickedStudent => PickedStudent is not null;
    public bool HasNoPickedStudent => PickedStudent is null;
    public string PickedStudentName => PickedStudent?.FullName ?? string.Empty;

    // ---------- أهلية الإلحاق ----------
    private string _annualStatusText = string.Empty;
    public string AnnualStatusText
    {
        get => _annualStatusText;
        private set => SetProperty(ref _annualStatusText, value);
    }

    private bool _annualMissing;
    public bool AnnualMissing
    {
        get => _annualMissing;
        private set
        {
            SetProperty(ref _annualMissing, value);
            OnPropertyChanged(nameof(ShowCreateAnnual));
            CreateAnnualCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>D-76: يظهر فقط حين ينقص التسجيل السنوي كلياً (وجوده بمستوى مخالف يُعدَّل من لوحة الطالب لا يُنشأ من جديد)</summary>
    public bool ShowCreateAnnual => PickedStudent is not null && AnnualMissing;

    private bool _canEnrollPicked;
    public bool CanEnrollPicked
    {
        get => _canEnrollPicked;
        private set
        {
            SetProperty(ref _canEnrollPicked, value);
            EnrollCommand.RaiseCanExecuteChanged();
        }
    }

    // ---------- السعر (D-77) ----------
    private string _suggestedPriceText = string.Empty;
    public string SuggestedPriceText
    {
        get => _suggestedPriceText;
        private set => SetProperty(ref _suggestedPriceText, value);
    }

    private string _agreedPriceText = string.Empty;
    public string AgreedPriceText
    {
        get => _agreedPriceText;
        set => SetProperty(ref _agreedPriceText, value);
    }

    private string _discountNote = string.Empty;
    public string DiscountNote
    {
        get => _discountNote;
        set => SetProperty(ref _discountNote, value);
    }

    // ---------- الحصص المبدئية (D-97: عرف الشهر = 4 · 0 = بلا شراء الآن) ----------
    private string _initialSessionsText = "4";
    public string InitialSessionsText
    {
        get => _initialSessionsText;
        set => SetProperty(ref _initialSessionsText, value);
    }

    // ---------- الخطأ والانشغال ----------
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); EnrollCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand ClearPickedCommand { get; }
    public AsyncRelayCommand CreateAnnualCommand { get; }
    public AsyncRelayCommand EnrollCommand { get; }
    public AsyncRelayCommand WithdrawCommand { get; }
    public AsyncRelayCommand TransferCommand { get; }

    // ---------- التهيئة ----------
    public async Task InitializeAsync(ClassGroupListItem group)
    {
        _group = group;
        OnPropertyChanged(nameof(GroupHeader));
        await LoadRosterAsync();
    }

    private async Task LoadRosterAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetClassGroupRosterHandler>();
        var result = await handler.ExecuteAsync(_group.Id);

        if (result.IsSuccess)
        {
            Roster.Clear();
            foreach (var item in result.Value!)
                Roster.Add(item);
            SelectedEnrollment = null;
            OnPropertyChanged(nameof(RosterEmpty));

            var activeCount = Roster.Count(r => r.Status == EnrollmentStatus.Active);
            EnrolledSummary = _group.Capacity is null
                ? $"النشطون الآن: {activeCount}"
                : $"النشطون الآن: {activeCount} / السعة: {_group.Capacity}";
        }
        else _notifier.ShowError(result.ErrorMessage!);
    }

    private async Task DebouncedStudentSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);

            if (string.IsNullOrWhiteSpace(StudentSearchText))
            {
                StudentResults.Clear();
                OnPropertyChanged(nameof(ShowStudentResults));
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SearchStudentsHandler>();
            var result = await handler.ExecuteAsync(StudentSearchText, cts.Token);

            if (result.IsSuccess)
            {
                StudentResults.Clear();
                // الفعّالون (شخصاً) غير المسجَّلين نشطين في هذا الفوج فقط قابلون للإلحاق
                var activeIdsInGroup = Roster.Where(r => r.Status == EnrollmentStatus.Active).Select(r => r.StudentId).ToHashSet();
                foreach (var s in result.Value!.Where(s => s.IsActive && !activeIdsInGroup.Contains(s.Id)).Take(8))
                    StudentResults.Add(s);
                OnPropertyChanged(nameof(ShowStudentResults));
            }
            else if (!cts.IsCancellationRequested)
                _notifier.ShowError(result.ErrorMessage!);
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69: قناة fire-and-forget محصّنة
        {
            _logger.LogError(ex, "Failed to search students for roster of class group {ClassGroupId}", _group?.Id);
            _notifier.ShowError("تعذّر البحث عن الطلاب — أعد المحاولة.");
        }
    }

    // يفحص: التسجيل السنوي النشط في سنة الفوج (D-54) ← تطابق المستوى ← شعبة ضمن شعب الفوج (D-59) ← اقتراح السعر (D-77)
    private async Task RefreshEligibilityAsync()
    {
        AnnualMissing = false;
        CanEnrollPicked = false;
        AnnualStatusText = string.Empty;
        SuggestedPriceText = string.Empty;
        AgreedPriceText = string.Empty;
        DiscountNote = string.Empty;
        InitialSessionsText = "4";   // D-97: الافتراضي يعود مع كل التقاط جديد
        ErrorMessage = null;

        var student = PickedStudent;
        if (student is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var annualsHandler = scope.ServiceProvider.GetRequiredService<GetAnnualEnrollmentsForStudentHandler>();
            var annualsResult = await annualsHandler.ExecuteAsync(student.Id);
            if (!annualsResult.IsSuccess)
            {
                _notifier.ShowError(annualsResult.ErrorMessage!);
                return;
            }

            var annual = annualsResult.Value!.FirstOrDefault(a => a.Status == EnrollmentStatus.Active && a.AcademicYearId == _group.AcademicYearId);
            if (annual is null)
            {
                AnnualStatusText = $"لا تسجيل سنوي نشط لـ«{student.FullName}» في سنة {_group.AcademicYearName} — أنشئه من هنا ثم أكمل الإلحاق:";
                AnnualMissing = true;
                return;
            }
            if (annual.LevelId != _group.LevelId)
            {
                AnnualStatusText = $"التسجيل السنوي موجود لكن مستواه «{annual.LevelName}» ≠ مستوى الفوج «{_group.LevelName}» — عدّله من لوحة الطالب في شاشة الطلاب.";
                return;
            }

            // D-59 مسبقاً في الواجهة — والـHandler يحرسه خلفياً
            var streamsHandler = scope.ServiceProvider.GetRequiredService<GetClassGroupStreamIdsHandler>();
            var streamsResult = await streamsHandler.ExecuteAsync(_group.Id);
            if (!streamsResult.IsSuccess)
            {
                _notifier.ShowError(streamsResult.ErrorMessage!);
                return;
            }
            if (streamsResult.Value!.Count > 0
                && (annual.StreamId is null || !streamsResult.Value!.Contains(annual.StreamId.Value)))
            {
                AnnualStatusText = $"الفوج مقيّد بشعب محددة، وشعبة الطالب في تسجيله السنوي «{annual.StreamDisplay}» ليست ضمنها.";
                return;
            }

            AnnualStatusText = $"التسجيل السنوي: موجود ✓ ({annual.LevelName} · {annual.StreamDisplay})";

            // السعر المقترح من جدول الأسعار (سنة/مستوى/مادة الفوج)
            var priceHandler = scope.ServiceProvider.GetRequiredService<GetSubjectPriceHandler>();
            var priceResult = await priceHandler.ExecuteAsync(_group.AcademicYearId, _group.LevelId, _group.SubjectId);
            if (!priceResult.IsSuccess)
            {
                _notifier.ShowError(priceResult.ErrorMessage!);
                return;
            }

            if (priceResult.Value is null)
            {
                SuggestedPriceText = "لا سعر في جدول الأسعار لهذا الفوج — الإدخال اليدوي إلزامي.";
            }
            else
            {
                SuggestedPriceText = $"سعر الجدول: {MoneyInput.FormatDinars(priceResult.Value.Value)} دج — اترك الحقل فارغاً ليُؤخذ كما هو، أو 0 = مجاني";
                AgreedPriceText = MoneyInput.FormatDinars(priceResult.Value.Value);
            }

            CanEnrollPicked = true;
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to refresh enrollment eligibility for student {StudentId} in class group {ClassGroupId}",
                student.Id, _group?.Id);
            _notifier.ShowError("تعذّر فحص أهلية الطالب — أعد اختياره.");
        }
    }

    // D-76: التدفق السريع — ديالوغ التسجيل السنوي القائم، بسنة الفوج مسبوقة الاختيار، ثم يُستأنف الإلحاق
    private async Task CreateAnnualAsync()
    {
        var student = PickedStudent;
        if (student is null) return;

        var editor = _services.GetRequiredService<AnnualEnrollmentEditorViewModel>();
        await editor.InitializeForCreateAsync(student, _group.AcademicYearId);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await RefreshEligibilityAsync();
    }

    private async Task EnrollAsync()
    {
        var student = PickedStudent;
        if (student is null || !CanEnrollPicked) return;

        ErrorMessage = null;

        // الفارغ = سعر الجدول كما هو (null للـHandler) · 0 صريح = مجاني
        long? agreedCentimes = null;
        if (!string.IsNullOrWhiteSpace(AgreedPriceText))
        {
            if (!MoneyInput.TryParseDinars(AgreedPriceText, out var parsed))
            {
                ErrorMessage = "أدخل سعراً صحيحاً بالدينار — والفارغ = سعر الجدول كما هو.";
                return;
            }
            agreedCentimes = parsed;
        }

        // D-97: الحصص المبدئية
        if (!int.TryParse(InitialSessionsText.Trim(), out var initialSessions) || initialSessions < 0)
        {
            ErrorMessage = "الحصص المبدئية يجب أن تكون رقماً صحيحاً — 0 = بلا شراء الآن.";
            return;
        }

        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<EnrollStudentInGroupHandler>();
            var result = await handler.ExecuteAsync(new EnrollStudentInGroupRequest(
                _group.Id, student.Id, agreedCentimes,
                string.IsNullOrWhiteSpace(DiscountNote) ? null : DiscountNote,
                initialSessions));

            if (result.IsSuccess)
            {
                _changed = true;
                _notifier.ShowSuccess(initialSessions > 0
                    ? $"أُلحق «{student.FullName}» بالفوج واشترى {initialSessions} حصص ✔"
                    : $"أُلحق «{student.FullName}» بالفوج ✔");
                PickedStudent = null;
                StudentSearchText = string.Empty;
                StudentResults.Clear();
                OnPropertyChanged(nameof(ShowStudentResults));
                await LoadRosterAsync();
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // قواعد متوقعة (ممتلئ/مكرر/شعبة…) ← بانر الديالوغ (D-22)
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task WithdrawAsync()
    {
        var enrollment = SelectedEnrollment;
        if (enrollment is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "انسحاب من الفوج",
            $"سيُسجَّل انسحاب «{enrollment.FullName}» من هذا الفوج. يبقى تسجيله السنوي نشطاً وتاريخه محفوظاً، ويمكن إعادة إلحاقه في أي وقت.",
            "تأكيد الانسحاب");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<WithdrawGroupEnrollmentHandler>();
        var result = await handler.ExecuteAsync(new WithdrawGroupEnrollmentRequest(enrollment.Id));

        if (result.IsSuccess)
        {
            _changed = true;
            _notifier.ShowSuccess($"سُجّل انسحاب «{enrollment.FullName}» ✔");
            await LoadRosterAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            ErrorMessage = result.ErrorMessage;
    }

    private async Task TransferAsync()
    {
        var enrollment = SelectedEnrollment;
        if (enrollment is null) return;

        var dialog = _services.GetRequiredService<TransferGroupEnrollmentViewModel>();
        await dialog.InitializeAsync(enrollment.Id, enrollment.FullName, _group.Name, enrollment.AgreedUnitPriceCentimes);   // D-84: تهيئة بالمعرّف — مشتركة بين المحورين

        if (await _dialogs.ShowDialogAsync(dialog, dialog.Title))
        {
            _changed = true;
            await LoadRosterAsync();
        }
    }
}