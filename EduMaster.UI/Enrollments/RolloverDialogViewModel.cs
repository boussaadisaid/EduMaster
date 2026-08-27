using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Enrollments;

/// <summary>خيارات كومبوبكسات ديالوغ الترحيل — StreamOption بمعرّف فارغ = «بلا شعبة» (D-59/D-60)</summary>
public sealed record RolloverYearOption(int Id, string Name);
public sealed record RolloverLevelOption(int Id, string Name);
public sealed record RolloverStreamOption(int? Id, string Label);

/// <summary>
/// ديالوغ «الترحيل الجماعي» (6.2 — D-129) بثلاث خطوات: الخريطة ← المعاينة ← التنفيذ والتقرير.
/// الهدف = السنة المحددة في شاشة السنوات · المصدر صريح بافتراضي الحالية (تر-2) · الحقوق = افتراضي الهدف للجميع (تر-4/D-66) ·
/// الافتراضي الذكي للخريطة: نفس المستوى الفعّال + نفس الشعبة/اسمها إن وُجدت · المستبعدون مرئيون بأسبابهم (تر-6 روح D-124).
/// </summary>
public sealed class RolloverDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly ILogger<RolloverDialogViewModel> _logger;

    private AcademicYearListItem _targetYear = null!;
    private long _targetFeeCentimes;
    private IReadOnlyList<RolloverLevelOption> _activeLevels = new List<RolloverLevelOption>();
    private readonly Dictionary<int, List<RolloverStreamOption>> _streamsByLevel = new();
    private readonly List<RolloverCandidateItem> _candidates = new();
    private bool _changed;

    public RolloverDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        IDialogService dialogs, ILogger<RolloverDialogViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _dialogs = dialogs;
        _logger = logger;

        CloseCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, _changed);
            return Task.CompletedTask;
        });
        NextCommand = new AsyncRelayCommand(() =>
        {
            CurrentStep = 2;
            return Task.CompletedTask;
        }, () => CurrentStep == 1 && MappingComplete && !IsBusy);
        BackCommand = new AsyncRelayCommand(() =>
        {
            CurrentStep = 1;
            return Task.CompletedTask;
        }, () => CurrentStep == 2 && !IsBusy);
        ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, () => CurrentStep == 2 && SelectedCount > 0 && MappingComplete && !IsBusy);
        AgainCommand = new AsyncRelayCommand(async () =>
        {
            // قراءة طازجة: الناجحون صاروا «في الهدف مسبقاً» فيُتخطَّون — تُكمل الفاشلين فقط (روح D-87)
            await LoadCandidatesAsync();
        }, () => CurrentStep == 3 && !IsBusy);
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _targetYear is null ? "الترحيل الجماعي بين السنوات" : $"الترحيل الجماعي إلى «{_targetYear.Name}»";

    // ---------- الخطوات ----------
    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            SetProperty(ref _currentStep, value);
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(StepIndicatorText));
            BackCommand.RaiseCanExecuteChanged();
            NextCommand.RaiseCanExecuteChanged();
            ExecuteCommand.RaiseCanExecuteChanged();
            AgainCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    public string StepIndicatorText => CurrentStep switch
    {
        1 => "الخطوة 1 من 3 — خريطة الانتقال بين المستويات والشعب",
        2 => "الخطوة 2 من 3 — معاينة المرشحين والتحديد",
        _ => "الخطوة 3 من 3 — تقرير التنفيذ",
    };

    // ---------- الترويسة ----------
    public string TargetHeaderText => _targetYear is null ? string.Empty
        : $"الهدف: «{_targetYear.Name}» · حقوق التسجيل المطبَّقة على الجميع: {MoneyInput.FormatDinars(_targetFeeCentimes)} دج (الإعفاءات تُعاد يدوياً من لوحة الطالب — تر-4)";

    // ---------- سنة المصدر (تر-2: صريحة بافتراضي الحالية) ----------
    public ObservableCollection<RolloverYearOption> SourceYearOptions { get; } = new();

    private RolloverYearOption? _selectedSourceYear;
    public RolloverYearOption? SelectedSourceYear
    {
        get => _selectedSourceYear;
        set
        {
            if (SetProperty(ref _selectedSourceYear, value) && value is not null && !IsBusy)
                _ = LoadCandidatesAsync();   // محصّنة داخلياً (D-69)
        }
    }

    // ---------- الخريطة (تر-3) ----------
    public ObservableCollection<MappingRowViewModel> MappingRows { get; } = new();

    public bool MappingComplete => MappingRows.Count > 0 && MappingRows.All(m => m.SelectedTargetLevel is not null);

    public bool MappingEmpty => MappingRows.Count == 0;

    // ---------- المعاينة (تر-5/تر-6) ----------
    public ObservableCollection<PreviewRowViewModel> PreviewRows { get; } = new();

    public int SelectedCount => PreviewRows.Count(r => r.IsSelected);

    private string _countersText = string.Empty;
    public string CountersText
    {
        get => _countersText;
        private set => SetProperty(ref _countersText, value);
    }

    // ---------- التقرير ----------
    public ObservableCollection<ResultRowViewModel> ResultRows { get; } = new();

    private string _resultSummaryText = string.Empty;
    public string ResultSummaryText
    {
        get => _resultSummaryText;
        private set => SetProperty(ref _resultSummaryText, value);
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
        private set
        {
            SetProperty(ref _isBusy, value);
            NextCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
            ExecuteCommand.RaiseCanExecuteChanged();
            AgainCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand ExecuteCommand { get; }
    public AsyncRelayCommand AgainCommand { get; }

    // ---------- التهيئة ----------
    public async Task InitializeAsync(AcademicYearListItem targetYear)
    {
        _targetYear = targetYear;
        OnPropertyChanged(nameof(Title));

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            // حقوق الهدف من الكيان — المسطّح لا يحملها (درس D-74)
            var yearHandler = scope.ServiceProvider.GetRequiredService<GetAcademicYearByIdHandler>();
            var yearResult = await yearHandler.ExecuteAsync(targetYear.Id);
            if (!yearResult.IsSuccess || yearResult.Value is null)
            {
                _notifier.ShowError(yearResult.ErrorMessage ?? "تعذّر تحميل السنة الهدف.");
                return;
            }
            _targetFeeCentimes = yearResult.Value.RegistrationFeeCentimes;
            OnPropertyChanged(nameof(TargetHeaderText));

            // سنوات المصدر: الفعّالة غير الهدف — الافتراضي الحالية (تر-2)
            var yearsHandler = scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>();
            var yearsResult = await yearsHandler.ExecuteAsync();
            if (!yearsResult.IsSuccess)
            {
                _notifier.ShowError(yearsResult.ErrorMessage!);
                return;
            }

            SourceYearOptions.Clear();
            foreach (var year in yearsResult.Value!.Where(y => y.IsActive && y.Id != targetYear.Id))
                SourceYearOptions.Add(new RolloverYearOption(year.Id, year.Name));

            if (SourceYearOptions.Count == 0)
            {
                ErrorMessage = "لا توجد سنة أخرى فعّالة تصلح مصدراً — أنشئها أو فعّلها من هذه الشاشة أولاً.";
                return;
            }

            // المستويات الفعّالة وشعبها تحميلاً مسبقاً (الجداول تافهة — روح D-33) فتعمل كومبوبكسات الخريطة تزامنياً
            var levelsHandler = scope.ServiceProvider.GetRequiredService<GetLevelsHandler>();
            var levelsResult = await levelsHandler.ExecuteAsync();
            if (!levelsResult.IsSuccess)
            {
                _notifier.ShowError(levelsResult.ErrorMessage!);
                return;
            }

            _activeLevels = levelsResult.Value!
                .Where(l => l.IsActive)
                .Select(l => new RolloverLevelOption(l.Id, l.Name))
                .ToList();

            if (_activeLevels.Count == 0)
            {
                ErrorMessage = "لا مستويات فعّالة تصلح هدفاً — فعّلها من شاشة البنية الأكاديمية أولاً.";
                return;
            }

            _streamsByLevel.Clear();
            var streamsHandler = scope.ServiceProvider.GetRequiredService<GetStreamsByLevelHandler>();
            foreach (var level in _activeLevels)
            {
                var streamsResult = await streamsHandler.ExecuteAsync(level.Id);
                if (!streamsResult.IsSuccess)
                {
                    _notifier.ShowError(streamsResult.ErrorMessage!);
                    return;
                }

                var options = new List<RolloverStreamOption> { new(null, "بلا شعبة") };
                options.AddRange(streamsResult.Value!.Where(s => s.IsActive).Select(s => new RolloverStreamOption(s.Id, s.Name)));
                _streamsByLevel[level.Id] = options;
            }

            // التحميل الأول للمرشحين عبر افتراضي المصدر (الحالية إن وُجدت)
            var currentYearId = yearsResult.Value!.FirstOrDefault(y => y.IsCurrent)?.Id;
            SelectedSourceYear = SourceYearOptions.FirstOrDefault(o => o.Id == currentYearId)
                ?? SourceYearOptions.FirstOrDefault();
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to initialize rollover dialog for target year {TargetYearId}", targetYear.Id);
            _notifier.ShowError("تعذّر تجهيز ديالوغ الترحيل — أغلقه وأعد فتحه.");
        }
    }

    // ---------- التحميل والبناء ----------
    private async Task LoadCandidatesAsync()
    {
        if (SelectedSourceYear is null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetRolloverCandidatesHandler>();
            var result = await handler.ExecuteAsync(SelectedSourceYear.Id, _targetYear.Id);

            if (!result.IsSuccess)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    ErrorMessage = result.ErrorMessage;   // تحقق متوقع ← بانر الديالوغ (D-22)
                return;
            }

            _candidates.Clear();
            _candidates.AddRange(result.Value!);
            RebuildMappingRows();
            RebuildPreviewRows();
            CurrentStep = 1;
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to load rollover candidates from year {SourceYearId} to year {TargetYearId}",
                SelectedSourceYear.Id, _targetYear.Id);
            _notifier.ShowError("تعذّر تحميل مرشحي الترحيل — أعد المحاولة.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildMappingRows()
    {
        MappingRows.Clear();

        // صفوف الخريطة تُشتق من تشكيلة المصدر الفعلية (مستوى + شعبة) — لا تخمين خارج الواقع
        foreach (var group in _candidates
            .GroupBy(c => (c.SourceLevelId, c.SourceStreamId))
            .OrderBy(g => g.First().SourceLevelName)
            .ThenBy(g => g.First().SourceStreamName))
        {
            var first = group.First();
            var row = new MappingRowViewModel(first.SourceLevelId, first.SourceStreamId,
                first.SourceLevelName, first.SourceStreamName,
                _activeLevels, StreamsForLevel, RecomputeTargets);

            // الافتراضي الذكي (تر-3): نفس المستوى إن كان فعّالاً، وإلا أول مستوى فعّال
            var defaultLevel = _activeLevels.FirstOrDefault(l => l.Id == first.SourceLevelId)
                ?? _activeLevels.FirstOrDefault();
            var streams = defaultLevel is null ? new List<RolloverStreamOption>() : StreamsForLevel(defaultLevel.Id);
            // نفس الشعبة بمعرّفها إن بقي الهدف مستواها، وإلا بنفس الاسم في المستوى الجديد، وإلا «بلا شعبة» (لا تخمين — روح D-59)
            var defaultStream = first.SourceStreamId is null
                ? streams.FirstOrDefault(s => s.Id is null)
                : streams.FirstOrDefault(s => s.Id == first.SourceStreamId)
                  ?? streams.FirstOrDefault(s => s.Label == first.SourceStreamName)
                  ?? streams.FirstOrDefault(s => s.Id is null);
            row.ApplyDefaults(defaultLevel, streams, defaultStream);
            MappingRows.Add(row);
        }

        OnPropertyChanged(nameof(MappingEmpty));
        RecomputeTargets();
    }

    private List<RolloverStreamOption> StreamsForLevel(int levelId)
        => _streamsByLevel.TryGetValue(levelId, out var streams) ? streams : new List<RolloverStreamOption> { new(null, "بلا شعبة") };

    private void RebuildPreviewRows()
    {
        PreviewRows.Clear();
        foreach (var candidate in _candidates)
            PreviewRows.Add(new PreviewRowViewModel(candidate, OnPreviewSelectionChanged));
        OnPreviewSelectionChanged();
    }

    /// <summary>تغيّر الخريطة ← يُعاد حساب عمود «إلى» لكل سطر معاينة فوراً</summary>
    private void RecomputeTargets()
    {
        foreach (var row in PreviewRows)
        {
            var mapping = MappingRows.FirstOrDefault(m =>
                m.SourceLevelId == row.Candidate.SourceLevelId && m.SourceStreamId == row.Candidate.SourceStreamId);
            row.TargetLabel = mapping?.SelectedTargetLevel is null
                ? "— بلا خريطة"
                : $"{mapping.SelectedTargetLevel.Name} — {mapping.SelectedTargetStream?.Label ?? "بلا شعبة"}";
        }

        OnPropertyChanged(nameof(MappingComplete));
        NextCommand.RaiseCanExecuteChanged();
        ExecuteCommand.RaiseCanExecuteChanged();
    }

    private void OnPreviewSelectionChanged()
    {
        CountersText = $"المرشحون: {_candidates.Count} · محدَّدون: {SelectedCount}"
            + $" · سيُتخطَّون (في الهدف مسبقاً): {_candidates.Count(c => c.AlreadyInTarget)}"
            + $" · مستبعدون: {_candidates.Count(c => !c.IsEligible)}";
        ExecuteCommand.RaiseCanExecuteChanged();
    }

    // ---------- التنفيذ ----------
    private async Task ExecuteAsync()
    {
        var selectedIds = PreviewRows.Where(r => r.IsSelected).Select(r => r.Candidate.StudentId).ToList();
        var mappings = MappingRows
            .Select(m => new RolloverMappingInput(m.SourceLevelId, m.SourceStreamId,
                m.SelectedTargetLevel!.Id, m.SelectedTargetStream?.Id))
            .ToList();

        var confirmed = await _dialogs.ConfirmAsync(
            "تنفيذ الترحيل الجماعي",
            $"سيُرحَّل {selectedIds.Count} طالباً من «{SelectedSourceYear!.Name}» إلى «{_targetYear.Name}» بحقوق {MoneyInput.FormatDinars(_targetFeeCentimes)} دج لكل واحد. " +
            "تُنشأ تسجيلات سنوية جديدة ومستحقات حقوقها ذرّياً (D-103) — بلا رجوع جماعي، والتصحيح فردي من لوحة الطالب.",
            $"ترحيل {selectedIds.Count} طالباً");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<BulkRolloverHandler>();
            var result = await handler.ExecuteAsync(
                new BulkRolloverRequest(SelectedSourceYear.Id, _targetYear.Id, mappings, selectedIds));

            if (!result.IsSuccess)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    ErrorMessage = result.ErrorMessage;   // خريطة/تحقق متوقع ← بانر الديالوغ (D-22)
                return;
            }

            var report = result.Value!;
            ResultRows.Clear();
            foreach (var row in report.Rows)
            {
                var name = _candidates.FirstOrDefault(c => c.StudentId == row.StudentId)?.FullName ?? $"#{row.StudentId}";
                ResultRows.Add(new ResultRowViewModel(name, row));
            }

            ResultSummaryText = $"✔ رُحّل: {report.SuccessCount} · ⏭ تُخطّي: {report.SkippedCount} · ✖ فشل: {report.FailedCount}";
            if (report.SuccessCount > 0)
            {
                _changed = true;
                _notifier.ShowSuccess($"اكتمل الترحيل: {report.SuccessCount} نجاح ✔ · تخطٍّ {report.SkippedCount} · فشل {report.FailedCount}");
            }
            else
            {
                _notifier.ShowWarning("لم يُرحَّل أحد — راجع سطور التقرير بالأسباب.");
            }

            CurrentStep = 3;
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69
        {
            _logger.LogError(ex, "Failed to execute bulk rollover from year {SourceYearId} to year {TargetYearId}",
                SelectedSourceYear.Id, _targetYear.Id);
            _notifier.ShowError("تعذّر تنفيذ الترحيل الجماعي — أعد المحاولة.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
