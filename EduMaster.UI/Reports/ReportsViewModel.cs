using EduMaster.UI.Common.MVVM;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Reports;

/// <summary>شاشة «📊 التقارير» (6.1 — D-127 · 6.4 — ق-1/ق-2/ق-5): قائمة تقارير ← عرض النتيجة · قراءة خالصة، لا كتابة من هنا أبداً</summary>
public sealed class ReportsViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;
    private PaymentMovementReportViewModel? _movement;
    private StudentStatementViewModel? _statement;
    private AttendanceSummaryReportViewModel? _attendance;
    private GroupSessionsReportViewModel? _groupSessions;
    private LowSessionBalancesViewModel? _lowBalances;

    public sealed record ReportOption(string Key, string Title);

    public ReportsViewModel(IServiceProvider services)
    {
        _services = services;
        Reports.Add(new ReportOption("movement", "🧾  حركة القبض لفترة"));
        Reports.Add(new ReportOption("statement", "📄  كشف حساب طالب"));
        // 6.4 — ق-ب: الأكاديمية والتنبيه
        Reports.Add(new ReportOption("attendance", "🟢  حضور الطلاب لفترة"));
        Reports.Add(new ReportOption("sessions", "📚  حصص الأفواج لفترة"));
        Reports.Add(new ReportOption("lowbalance", "⏳  تنبيه نفاد الأرصدة"));
    }

    public ObservableCollection<ReportOption> Reports { get; } = new();

    private ReportOption? _selectedReport;
    public ReportOption? SelectedReport
    {
        get => _selectedReport;
        set
        {
            if (SetProperty(ref _selectedReport, value))
                SwitchReport();
        }
    }

    private object? _currentReport;
    public object? CurrentReport
    {
        get => _currentReport;
        private set => SetProperty(ref _currentReport, value);
    }

    public async Task InitializeAsync()
    {
        // نسخ تُحفظ — التنقل بين التقارير لا يفقد حالتها
        _movement = _services.GetRequiredService<PaymentMovementReportViewModel>();
        _statement = _services.GetRequiredService<StudentStatementViewModel>();
        _attendance = _services.GetRequiredService<AttendanceSummaryReportViewModel>();
        _groupSessions = _services.GetRequiredService<GroupSessionsReportViewModel>();
        _lowBalances = _services.GetRequiredService<LowSessionBalancesViewModel>();
        await _movement.InitializeAsync();     // تقرير اليوم جاهز عند الفتح
        await _statement.InitializeAsync();    // قائمة الطلاب الأولية جاهزة للانتقاء
        await _attendance.InitializeAsync();   // 6.4: الشهر الجاري + فلتر الأفواج
        await _groupSessions.InitializeAsync();
        await _lowBalances.InitializeAsync();  // التنبيه جاهز فوراً — الأرقام أول ما يُطلب صباحاً
        SelectedReport = Reports[0];
    }

    private void SwitchReport()
    {
        CurrentReport = SelectedReport?.Key switch
        {
            "statement" => _statement,
            "attendance" => _attendance,
            "sessions" => _groupSessions,
            "lowbalance" => _lowBalances,
            _ => _movement,
        };
    }
}
