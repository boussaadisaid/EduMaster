using EduMaster.Application.AcademicYears;
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.Application.Reports;
using EduMaster.Application.Settings;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Reports;

/// <summary>
/// تقرير حركة القبض لفترة (6.1 — D-127): فلترا تاريخ افتراضهما اليوم (روح D-94) + إجماليات مشتقة + سجل الإيصالات.
/// 6.3 (ط-هـ): زر 🖨 يطبع آخر نتيجة معروضة حرفياً (WYSIWYP) بترويسة هوية المدرسة (ط-7).
/// </summary>
public sealed class PaymentMovementReportViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<PaymentMovementReportViewModel> _logger;
    private readonly IPrintService _printService;
    private CancellationTokenSource? _loadCts;
    private PaymentMovementReportItem? _lastReport;
    private AcademicYearListItem? _selectedAcademicYear;

    public PaymentMovementReportViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<PaymentMovementReportViewModel> logger, IPrintService printService)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _printService = printService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => _lastReport is not null);
    }

    public ObservableCollection<PaymentListItem> Rows { get; } = new();

    public ObservableCollection<AcademicYearListItem> AcademicYears { get; } = new();

    public AcademicYearListItem? SelectedAcademicYear
    {
        get => _selectedAcademicYear;
        set
        {
            if (!SetProperty(ref _selectedAcademicYear, value) || value is null)
                return;

            _loadCts?.Cancel();
            SetDateRangeSilently(value);
            _ = LoadAsync();
        }
    }

    private void SetDateRangeSilently(AcademicYearListItem year)
    {
        var from = year.StartDate.ToDateTime(TimeOnly.MinValue);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var effectiveTo = today < year.EndDate ? today : year.EndDate;

        if (effectiveTo < year.StartDate)
            effectiveTo = year.EndDate;

        _fromDate = from;
        _toDate = effectiveTo.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
    }

    private DateTime? _fromDate;
    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
                Reload();
        }
    }

    private DateTime? _toDate;
    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
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
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var yearsHandler = scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>();
            var yearsResult = await yearsHandler.ExecuteAsync();

            if (yearsResult.IsSuccess)
            {
                AcademicYears.Clear();
                foreach (var year in yearsResult.Value!
                    .OrderByDescending(y => y.StartDate))
                {
                    AcademicYears.Add(year);
                }

                _selectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent)
                    ?? AcademicYears.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedAcademicYear));

                if (_selectedAcademicYear is not null)
                    SetDateRangeSilently(_selectedAcademicYear);
            }
            else
            {
                _notifier.ShowWarning(yearsResult.ErrorMessage!);
                _fromDate = DateTime.Today;
                _toDate = DateTime.Today;
                OnPropertyChanged(nameof(FromDate));
                OnPropertyChanged(nameof(ToDate));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize academic-year filter for payment movement report");
            _fromDate = DateTime.Today;
            _toDate = DateTime.Today;
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
        }

        await LoadAsync();
    }

    private void Reload()
    {
        _loadCts?.Cancel();   // D-64: تبديل الفلاتر فوري — يلغي التحميل السابق
        var cts = _loadCts = new CancellationTokenSource();
        _ = LoadAsync(cts.Token);
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetPaymentMovementReportHandler>();
            var result = await handler.ExecuteAsync(
                DateOnly.FromDateTime(FromDate.Value), DateOnly.FromDateTime(ToDate.Value), cancellationToken);

            if (result.IsSuccess)
            {
                var report = result.Value!;
                _lastReport = report;   // ط-هـ: تُحفظ آخر نتيجة معروضة — الطباعة تلتقط ما على الشاشة حرفياً (WYSIWYP)
                PrintCommand.RaiseCanExecuteChanged();

                Rows.Clear();
                foreach (var row in report.Rows)
                    Rows.Add(row);

                SummaryText =
                    $"قبض: {MoneyInput.FormatDinars(report.ReceiptsTotalCentimes)} دج ({report.ReceiptsCount})" +
                    $" · صرف: {MoneyInput.FormatDinars(report.RefundsTotalCentimes)} دج ({report.RefundsCount})" +
                    $" · الصافي: {MoneyInput.FormatDinars(report.NetCentimes)} دج" +
                    $" · غير مخصص: {MoneyInput.FormatDinars(report.UnallocatedTotalCentimes)} دج";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);   // «من بعد إلى» — شاشة تقارير ← تحذيري (D-29)
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69: قناة fire-and-forget محصّنة
        {
            _logger.LogError(ex, "Failed to load payment movement report");
            _notifier.ShowError("تعذّر تحميل تقرير حركة القبض — أعد المحاولة.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 🖨 طباعة التقرير (6.3 — ط-هـ): آخر نتيجة معروضة + ترويسة الهوية ← المرسّم ← نافذة الطباعة.
    /// إلغاء النافذة بصمت · فشلها toast عربي + تسجيل إنجليزي (D-22/D-69).
    /// </summary>
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

            if (_printService.PrintPaymentMovement(new PaymentMovementPrintModel(header, report)) == PrintOutcome.Failed)
                _notifier.ShowError("تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to print payment movement report");
            _notifier.ShowError("تعذّرت طباعة التقرير — أعد المحاولة.");
        }
    }
}
