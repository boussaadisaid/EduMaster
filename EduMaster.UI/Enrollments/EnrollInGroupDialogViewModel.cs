using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Pricing;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Enrollments;

/// <summary>
/// ديالوغ «إلحاق بفوج» الطالب-المحوري (D-83): الأفواج المؤهَّلة فقط + إلحاق متتالٍ بلا إغلاق
/// (عدة مواد بجلسة واحدة) + تدفق سريع للتسجيل السنوي (D-76) + سعر مقترح من الجدول (D-77)
/// + حصص مبدئية بافتراضي 4 في معاملة الإلحاق (D-97)
/// </summary>
public sealed class EnrollInGroupDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly ILogger<EnrollInGroupDialogViewModel> _logger;

    private StudentListItem _student = null!;
    private bool _changed;

    public EnrollInGroupDialogViewModel(IServiceScopeFactory scopeFactory, IServiceProvider services,
        IUserNotifier notifier, IDialogService dialogs, ILogger<EnrollInGroupDialogViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;
        _logger = logger;

        EnrollCommand = new AsyncRelayCommand(EnrollAsync, () => SelectedGroup is not null && !IsBusy);
        CreateAnnualCommand = new AsyncRelayCommand(CreateAnnualAsync, () => AnnualMissing);
        CloseCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, _changed);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => "إلحاق بفوج";

    public string StudentName => _student?.FullName ?? string.Empty;

    // ---------- التسجيل السنوي ----------
    private string _annualSummary = string.Empty;
    public string AnnualSummary
    {
        get => _annualSummary;
        private set => SetProperty(ref _annualSummary, value);
    }

    private bool _annualMissing;
    public bool AnnualMissing
    {
        get => _annualMissing;
        private set
        {
            SetProperty(ref _annualMissing, value);
            CreateAnnualCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(EligibleGroupsEmpty));
        }
    }

    // ---------- الأفواج المؤهَّلة ----------
    public ObservableCollection<ClassGroupListItem> EligibleGroups { get; } = new();

    private ClassGroupListItem? _selectedGroup;
    public ClassGroupListItem? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            SetProperty(ref _selectedGroup, value);
            OnPropertyChanged(nameof(HasSelectedGroup));
            EnrollCommand.RaiseCanExecuteChanged();
            _ = LoadSuggestedPriceAsync();
        }
    }

    public bool HasSelectedGroup => SelectedGroup is not null;
    public bool EligibleGroupsEmpty => !AnnualMissing && EligibleGroups.Count == 0;

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

    public AsyncRelayCommand EnrollCommand { get; }
    public AsyncRelayCommand CreateAnnualCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }

    // ---------- التهيئة ----------
    public async Task InitializeAsync(StudentListItem student)
    {
        _student = student;
        OnPropertyChanged(nameof(StudentName));
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        AnnualSummary = string.Empty;
        EligibleGroups.Clear();
        SelectedGroup = null;
        SuggestedPriceText = string.Empty;
        AgreedPriceText = string.Empty;
        DiscountNote = string.Empty;
        InitialSessionsText = "4";   // D-97: الافتراضي يعود مع كل إلحاق متتالٍ
        ErrorMessage = null;

        await using var scope = _scopeFactory.CreateAsyncScope();

        var annualsHandler = scope.ServiceProvider.GetRequiredService<GetAnnualEnrollmentsForStudentHandler>();
        var annualsResult = await annualsHandler.ExecuteAsync(_student.Id);
        if (!annualsResult.IsSuccess)
        {
            _notifier.ShowError(annualsResult.ErrorMessage!);
            return;
        }

        var activeAnnuals = annualsResult.Value!.Where(a => a.Status == EnrollmentStatus.Active).ToList();
        AnnualMissing = activeAnnuals.Count == 0;
        if (AnnualMissing)
        {
            AnnualSummary = "لا تسجيل سنوي نشط لهذا الطالب — أنشئه أولاً:";
            return;
        }

        // D-71: قد توجد سنوات نشطة متعددة — المطابقة على أيٍّ منها في الاستعلام
        AnnualSummary = "التسجيل النشط: " + string.Join(" · ",
            activeAnnuals.Select(a => $"{a.AcademicYearName} — {a.LevelName} · {a.StreamDisplay}"));

        var groupsHandler = scope.ServiceProvider.GetRequiredService<GetEnrollableGroupsForStudentHandler>();
        var groupsResult = await groupsHandler.ExecuteAsync(_student.Id);
        if (!groupsResult.IsSuccess)
        {
            _notifier.ShowError(groupsResult.ErrorMessage!);
            return;
        }

        EligibleGroups.Clear();
        foreach (var group in groupsResult.Value!)
            EligibleGroups.Add(group);
        OnPropertyChanged(nameof(EligibleGroupsEmpty));
    }

    private async Task LoadSuggestedPriceAsync()
    {
        SuggestedPriceText = string.Empty;
        AgreedPriceText = string.Empty;

        var group = SelectedGroup;
        if (group is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetSubjectPriceHandler>();
            var result = await handler.ExecuteAsync(group.AcademicYearId, group.LevelId, group.SubjectId);

            if (!result.IsSuccess)
            {
                _notifier.ShowError(result.ErrorMessage!);
                return;
            }

            if (result.Value is null)
            {
                SuggestedPriceText = "لا سعر في جدول الأسعار لهذا الفوج — الإدخال اليدوي إلزامي.";
            }
            else
            {
                SuggestedPriceText = $"سعر الجدول: {MoneyInput.FormatDinars(result.Value.Value)} دج — اترك الحقل فارغاً ليُؤخذ كما هو، أو 0 = مجاني";
                AgreedPriceText = MoneyInput.FormatDinars(result.Value.Value);
            }
        }
        catch (Exception ex)   // D-69: قناة fire-and-forget محصّنة
        {
            _logger.LogError(ex, "Failed to load price suggestion for class group {ClassGroupId}", group.Id);
            _notifier.ShowError("تعذّر جلب سعر الفوج — أدخله يدوياً.");
        }
    }

    // D-76: التدفق السريع — ديالوغ التسجيل السنوي القائم، ثم تُستأنف الأهلية
    private async Task CreateAnnualAsync()
    {
        var editor = _services.GetRequiredService<AnnualEnrollmentEditorViewModel>();
        await editor.InitializeForCreateAsync(_student);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
        {
            _changed = true;
            await RefreshAsync();
        }
    }

    private async Task EnrollAsync()
    {
        var group = SelectedGroup;
        if (group is null) return;

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
                group.Id, _student.Id, agreedCentimes,
                string.IsNullOrWhiteSpace(DiscountNote) ? null : DiscountNote,
                initialSessions));

            if (result.IsSuccess)
            {
                _changed = true;
                _notifier.ShowSuccess(initialSessions > 0
                    ? $"أُلحق «{_student.FullName}» بفوج «{group.Name}» واشترى {initialSessions} حصص ✔"
                    : $"أُلحق «{_student.FullName}» بفوج «{group.Name}» ✔");
                // D-83: إلحاق متتالٍ — الديالوغ لا يُغلق، والفوج يختفي من المؤهَّلة فيلتحق بالمادة التالية فوراً
                await RefreshAsync();
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;   // قواعد متوقعة ← بانر الديالوغ (D-22)
        }
        finally
        {
            IsBusy = false;
        }
    }
}