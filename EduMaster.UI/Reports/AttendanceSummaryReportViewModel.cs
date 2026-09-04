using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.Application.Reports;
using EduMaster.Application.Settings;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Reports;

/// <summary>
/// تقرير حضور الطلاب لفترة (6.4 — ق-1): فلترا تاريخ افتراضهما الشهر الجاري + فلتر فوج اختياري · الأكثر غياباً أولاً ·
/// 🖨 يطبع آخر نتيجة معروضة حرفياً (WYSIWYP — ق-6) بترويسة الهوية · قراءة خالصة بإلغاء فوري (D-64).
/// </summary>
public sealed class AttendanceSummaryReportViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<AttendanceSummaryReportViewModel> _logger;
    private readonly IPrintService _printService;
    private CancellationTokenSource? _loadCts;
    private AttendanceSummaryReportItem? _lastReport;

    public sealed record GroupOption(int? Id, string Name);

    public AttendanceSummaryReportViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<AttendanceSummaryReportViewModel> logger, IPrintService printService)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _printService = printService;

        RefreshCommand = new AsyncRelayCommand(() => LoadPeriodAsync());
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => _lastReport is not null);
    }

    public ObservableCollection<AttendanceSummaryItem> Rows { get; } = new();
    public ObservableCollection<GroupOption> GroupOptions { get; } = new();

    private DateTime? _fromDate;
    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
                ReloadPeriod();
        }
    }

    private DateTime? _toDate;
    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
                ReloadPeriod();
        }
    }

    private GroupOption? _selectedGroup;
    public GroupOption? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
                Reload();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && Rows.Count == 0;

    private string _summaryText = string.Empty;
    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand PrintCommand { get; }

    public async Task InitializeAsync()
    {
        // الافتراضي: الشهر الجاري — حقلاً مباشرةً بلا إطلاق مزدوج (نمط شاشة الحصص)
        _fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _toDate = DateTime.Today;
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));

        await LoadPeriodAsync();
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetAttendanceSummaryHandler>();
        var result = await handler.GetAvailableGroupsAsync(
            DateOnly.FromDateTime(FromDate.Value), DateOnly.FromDateTime(ToDate.Value), cancellationToken);

        if (!result.IsSuccess)
        {
            if (!cancellationToken.IsCancellationRequested)
                _notifier.ShowError(result.ErrorMessage!);
            return;
        }

        var previousId = SelectedGroup?.Id;
        GroupOptions.Clear();
        GroupOptions.Add(new GroupOption(null, "كل الأفواج"));
        foreach (var group in result.Value!)
        {
            GroupOptions.Add(new GroupOption(group.Id, group.GroupName));
        }

        var next = previousId is null
            ? GroupOptions[0]
            : GroupOptions.FirstOrDefault(g => g.Id == previousId) ?? GroupOptions[0];

        if (!Equals(_selectedGroup, next))
        {
            _selectedGroup = next;
            OnPropertyChanged(nameof(SelectedGroup));
        }
    }

    private void ReloadPeriod()
    {
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        _ = LoadPeriodAsync(cts.Token);
    }

    private void Reload()
    {
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        _ = LoadAsync(cts.Token);
    }

    private async Task LoadPeriodAsync(CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        try
        {
            await LoadGroupsAsync(cancellationToken);
            await LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh attendance report period");
            _notifier.ShowError("تعذّر تحديث فلاتر التقرير — أعد المحاولة.");
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetAttendanceSummaryHandler>();
            var result = await handler.ExecuteAsync(
                DateOnly.FromDateTime(FromDate.Value), DateOnly.FromDateTime(ToDate.Value),
                SelectedGroup?.Id, cancellationToken);

            if (result.IsSuccess)
            {
                var report = result.Value!;
                _lastReport = report;   // ق-6: تُحفظ آخر نتيجة معروضة — الطباعة تلتقط ما على الشاشة حرفياً (WYSIWYP)
                PrintCommand.RaiseCanExecuteChanged();

                Rows.Clear();
                foreach (var row in report.Rows)
                    Rows.Add(row);

                SummaryText =
                    $"حاضر: {report.PresentTotal} · غائب: {report.AbsentTotal} · مبرر: {report.JustifiedTotal}" +
                    $" · نسبة الحضور: {report.OverallPercentText}";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);   // «من بعد إلى» — تحذيري (D-29)
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69: قناة fire-and-forget محصّنة
        {
            _logger.LogError(ex, "Failed to load attendance summary report");
            _notifier.ShowError("تعذّر تحميل تقرير الحضور — أعد المحاولة.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>🖨 طباعة التقرير المعروض حرفياً (ق-6) — المسار الجدولي العام · إلغاء النافذة بصمت · فشلها toast عربي + تسجيل إنجليزي (D-22/D-69)</summary>
    private async Task PrintAsync()
    {
        var report = _lastReport;
        if (report is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var school = await scope.ServiceProvider.GetRequiredService<GetSchoolInfoHandler>().ExecuteAsync();
            var info = school.IsSuccess ? school.Value! : new SchoolInfoItem(0, string.Empty, null, null, null);
            var header = new PrintHeader(info.DisplayName, info.Phone, info.Address, info.LogoPath);

            var groupSuffix = SelectedGroup is { Id: not null } selected ? $" · {selected.Name}" : string.Empty;
            var model = new TabularReportPrintModel(
                header,
                "تقرير حضور الطلاب",
                $"الفترة: من {report.From:yyyy-MM-dd} إلى {report.To:yyyy-MM-dd}{groupSuffix} · المبرر خارج نسبة الحضور",
                $"حاضر: {report.PresentTotal} · غائب: {report.AbsentTotal} · مبرر: {report.JustifiedTotal} · نسبة الحضور: {report.OverallPercentText}",
                new[]
                {
                    new TabularReportColumn("الطالب", 3.0),
                    new TabularReportColumn("الفوج", 2.2),
                    new TabularReportColumn("حاضر", 0.9),
                    new TabularReportColumn("غائب", 0.9),
                    new TabularReportColumn("مبرر", 0.9),
                    new TabularReportColumn("نسبة الحضور", 1.1),
                },
                report.Rows.Select(r => (IReadOnlyList<string>)new[]
                {
                    r.StudentName,
                    r.GroupName,
                    r.PresentCount.ToString(),
                    r.AbsentCount.ToString(),
                    r.JustifiedCount.ToString(),
                    r.AttendancePercentText,
                }).ToList());

            if (_printService.PrintA4Report(model) == PrintOutcome.Failed)
                _notifier.ShowError("تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to print attendance summary report");
            _notifier.ShowError("تعذّرت طباعة التقرير — أعد المحاولة.");
        }
    }
}
