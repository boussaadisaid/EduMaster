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
using System.Diagnostics;

namespace EduMaster.UI.Reports;

/// <summary>
/// تنبيه نفاد أرصدة الحصص (6.4 — ق-5/ق-9): عتبة قابلة (افتراضي 2 = نصف شهر — D-91) والأنفد أولاً ·
/// 💬 واتساب لكل سطر برسالة عربية جاهزة عبر رابط wa.me — الإنسان يراجع قبل الإرسال، صفر تكلفة وحزم (بلا هاتف صالح يتعطّل الزر) ·
/// 🖨 ورقة الاتصالات A4 (ق-6) · قراءة خالصة بإلغاء فوري (D-64).
/// </summary>
public sealed class LowSessionBalancesViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<LowSessionBalancesViewModel> _logger;
    private readonly IPrintService _printService;
    private CancellationTokenSource? _loadCts;
    private IReadOnlyList<LowSessionBalanceItem> _lastItems = Array.Empty<LowSessionBalanceItem>();

    public LowSessionBalancesViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<LowSessionBalancesViewModel> logger, IPrintService printService)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _printService = printService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => _lastItems.Count > 0);
        OpenWhatsAppCommand = new RelayCommand<LowSessionBalanceItem>(OpenWhatsApp, item => item?.ContactPhone is not null);
    }

    public ObservableCollection<LowSessionBalanceItem> Rows { get; } = new();

    private string _thresholdText = "2";   // العتبة الافتراضية = نصف شهر (عرف الشهر 4 حصص — D-91)
    public string ThresholdText
    {
        get => _thresholdText;
        set
        {
            if (SetProperty(ref _thresholdText, value))
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
    public RelayCommand<LowSessionBalanceItem> OpenWhatsAppCommand { get; }

    public Task InitializeAsync() => LoadAsync();

    private void Reload()
    {
        _loadCts?.Cancel();   // D-64
        var cts = _loadCts = new CancellationTokenSource();
        _ = LoadAsync(cts.Token);
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!int.TryParse((_thresholdText ?? string.Empty).Trim(), out var threshold))
            return;   // نص غير مكتمل أثناء الكتابة — لا تحميل ولا تنبيه

        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetLowSessionBalancesHandler>();
            var result = await handler.ExecuteAsync(threshold, cancellationToken);

            if (result.IsSuccess)
            {
                _lastItems = result.Value!;
                PrintCommand.RaiseCanExecuteChanged();

                Rows.Clear();
                foreach (var item in _lastItems)
                    Rows.Add(item);

                SummaryText = $"عدد التنبيهات: {_lastItems.Count} (الرصيد ≤ {threshold})";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);   // عتبة سالبة — تحذيري (D-29)
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69: قناة fire-and-forget محصّنة
        {
            _logger.LogError(ex, "Failed to load low session balances");
            _notifier.ShowError("تعذّر تحميل تنبيه الأرصدة — أعد المحاولة.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>💬 فتح واتساب برسالة تذكير جاهزة للمراجعة قبل الإرسال (ق-9) — التطبيع: 0… ← 213… عبر PhoneNumberNormalizer</summary>
    private void OpenWhatsApp(LowSessionBalanceItem? item)
    {
        if (item?.ContactPhone is null)
            return;

        var international = PhoneNumberNormalizer.ToWhatsAppInternational(item.ContactPhone);
        if (international is null)
        {
            _notifier.ShowWarning($"تعذّر تأهيل رقم «{item.ContactPhone}» لواتساب — راجع بطاقة المعني.");
            return;
        }

        try
        {
            var message =
                "السلام عليكم ورحمة الله، معكم إدارة المدرسة.\n" +
                $"نذكّركم بأن رصيد حصص «{item.StudentName}» في فوج {item.GroupName} ({item.SubjectName}) أوشك على النفاد — المتبقي: {item.Balance}.\n" +
                "نرجو التواصل مع الإدارة لتجديد الاشتراك، وشكراً لثقتكم.";
            Process.Start(new ProcessStartInfo(
                $"https://wa.me/{international}?text={Uri.EscapeDataString(message)}")
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open WhatsApp for enrollment {EnrollmentId}", item.EnrollmentId);
            _notifier.ShowError("تعذّر فتح واتساب — تحقق من تثبيته أو اتصل هاتفياً.");
        }
    }

    /// <summary>🖨 ورقة الاتصالات (ق-6): آخر قائمة معروضة حرفياً + الترويسة — WYSIWYP</summary>
    private async Task PrintAsync()
    {
        var items = _lastItems;
        if (items.Count == 0)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var school = await scope.ServiceProvider.GetRequiredService<GetSchoolInfoHandler>().ExecuteAsync();
            var info = school.IsSuccess ? school.Value! : new SchoolInfoItem(0, string.Empty, null, null, null);
            var header = new PrintHeader(info.DisplayName, info.Phone, info.Address, info.LogoPath);

            var model = new TabularReportPrintModel(
                header,
                "تنبيه نفاد أرصدة الحصص",
                $"بتاريخ {DateTime.Today:yyyy-MM-dd} · الأنفد أولاً — السالب = تجاوز على الرصيد",
                $"عدد التنبيهات: {items.Count} (العتبة: {_thresholdText})",
                new[]
                {
                    new TabularReportColumn("الطالب", 2.6),
                    new TabularReportColumn("الفوج", 1.8),
                    new TabularReportColumn("المادة", 1.5),
                    new TabularReportColumn("الرصيد", 0.8),
                    new TabularReportColumn("جهة التذكير", 2.2),
                    new TabularReportColumn("الهاتف", 1.3),
                },
                items.Select(i => (IReadOnlyList<string>)new[]
                {
                    i.StudentName,
                    i.GroupName,
                    i.SubjectName,
                    i.Balance.ToString(),
                    i.ContactName,
                    i.ContactPhone ?? "—",
                }).ToList());

            if (_printService.PrintA4Report(model) == PrintOutcome.Failed)
                _notifier.ShowError("تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to print low session balances");
            _notifier.ShowError("تعذّرت طباعة التقرير — أعد المحاولة.");
        }
    }
}
