using EduMaster.Application.Backup;
using EduMaster.UI.Academic;
using EduMaster.UI.AcademicYears;
using EduMaster.UI.Billing;
using EduMaster.UI.ClassGroups;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Employees;
using EduMaster.UI.Expenses;
using EduMaster.UI.Payroll;
using EduMaster.UI.People;
using EduMaster.UI.Reports;
using EduMaster.UI.Scheduling;
using EduMaster.UI.Students;
using EduMaster.UI.Teachers;
using EduMaster.UI.Treasury;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduMaster.UI
{
    public sealed class MainWindowViewModel : BaseViewModel
    {
        private readonly IServiceProvider _services;
        public MainWindowViewModel(IServiceProvider services)
        {
            _services = services;
            NavigateToHomeCommand = new AsyncRelayCommand(() =>
            {
                CurrentViewModel = _services.GetRequiredService<HomeViewModel>();
                CurrentScreenKey = "Home";
                return Task.CompletedTask;
            });
            NavigateToAcademicYearsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<AcademicYearsViewModel>();   // نسخة طازجة = بيانات طازجة
                CurrentViewModel = vm;
                CurrentScreenKey = "AcademicYears";
                await vm.InitializeAsync();
            });
            NavigateToPeopleCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<PeopleViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "People";
                await vm.InitializeAsync();
            });
            NavigateToStudentsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<StudentsViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Students";
                await vm.InitializeAsync();
            });
            NavigateToTeachersCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<TeachersViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Teachers";
                await vm.InitializeAsync();
            });
            // F5 — الشريحة 5.1: الموظفون (بند رئيسي — ب-1)
            NavigateToEmployeesCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<EmployeesViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Employees";
                await vm.InitializeAsync();
            });
            NavigateToClassGroupsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<ClassGroupsViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "ClassGroups";
                await vm.InitializeAsync();
            });
            // F3 — الشريحة 3.1: جدول استعمال الزمن + الحصص (بندان رئيسيان — عمل يومي لا إعداد)
            NavigateToTimetableCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<TimetableViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Timetable";
                await vm.InitializeAsync();
            });
            NavigateToSessionsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<SessionsViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Sessions";
                await vm.InitializeAsync();
            });
            // F4 — الشريحة 4.3: المالية (ديون + سجل مدفوعات — عمل يومي)
            NavigateToFinanceCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<FinanceViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Finance";
                await vm.InitializeAsync();
            });
            // المصاريف التشغيلية
            NavigateToExpensesCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<ExpenseViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Expenses";
                await vm.InitializeAsync();
            });
            // Treasury — الخزينة
            NavigateToTreasuryCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<TreasuryViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Treasury";
                await vm.InitializeAsync();
            });
            // F5 — الشريحة 5.2: الأجور (الاحتساب والاعتماد — D-116)   // جديد هـ-2
            NavigateToPayrollCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<PayrollRunsViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Payroll";
                await vm.InitializeAsync();
            });
            // F6 — الشريحة 6.1: التقارير (D-127)
            NavigateToReportsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<ReportsViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "Reports";
                await vm.InitializeAsync();
            });
            NavigateToAcademicStructureCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<AcademicStructureViewModel>();
                CurrentViewModel = vm;
                CurrentScreenKey = "AcademicStructure";
                await vm.InitializeAsync();
            });
            CurrentViewModel = _services.GetRequiredService<HomeViewModel>();      // الشاشة الافتتاحية

            // 6.5 — ن-4: تذكير النسخ الاحتياطي عند الدخول — قناة fire-and-forget محصّنة (D-69)
            _ = CheckBackupReminderAsync();
        }
        private object? _currentViewModel;
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
        }

        // F7: مفتاح الشاشة الحالية — يضيء بندها في الشريط الجانبي (يُسنَد في كل أمر تنقل)
        private string _currentScreenKey = "Home";
        public string CurrentScreenKey
        {
            get => _currentScreenKey;
            private set => SetProperty(ref _currentScreenKey, value);
        }
        public AsyncRelayCommand NavigateToHomeCommand { get; }
        public AsyncRelayCommand NavigateToAcademicYearsCommand { get; }
        public AsyncRelayCommand NavigateToPeopleCommand { get; }
        public AsyncRelayCommand NavigateToStudentsCommand { get; }
        public AsyncRelayCommand NavigateToTeachersCommand { get; }
        public AsyncRelayCommand NavigateToEmployeesCommand { get; }
        public AsyncRelayCommand NavigateToClassGroupsCommand { get; }
        public AsyncRelayCommand NavigateToTimetableCommand { get; }
        public AsyncRelayCommand NavigateToSessionsCommand { get; }
        public AsyncRelayCommand NavigateToFinanceCommand { get; }
        public AsyncRelayCommand NavigateToPayrollCommand { get; }
        public AsyncRelayCommand NavigateToExpensesCommand { get; }   // جديد هـ-2
        public AsyncRelayCommand NavigateToTreasuryCommand { get; }
        public AsyncRelayCommand NavigateToReportsCommand { get; }   // جديد 6.1-ج
        public AsyncRelayCommand NavigateToAcademicStructureCommand { get; }

        /// <summary>تذكير النسخ الاحتياطي عند الدخول (6.5 — ن-4): لا نسخة أبداً أو مضى عليها >7 أيام ← تحذيري — القرار في السياسة النقية المختبَرة · فشل الفحص يُسجَّل إنجليزياً ولا يُزعج الدخول (D-69)</summary>
        private async Task CheckBackupReminderAsync()
        {
            try
            {
                var scopeFactory = _services.GetRequiredService<IServiceScopeFactory>();
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<GetBackupStatusHandler>().ExecuteAsync();
                if (result.IsSuccess && result.Value!.ReminderDue)
                    _services.GetRequiredService<IUserNotifier>().ShowWarning(result.Value.LastBackupAtUtc is null
                        ? "لا توجد نسخة احتياطية بعد — أنشئ أول نسخة من «الإعدادات ← 💾 النسخ الاحتياطي»."
                        : "مرت أكثر من 7 أيام على آخر نسخة احتياطية — أنشئ نسخة جديدة من «الإعدادات ← 💾 النسخ الاحتياطي».");
            }
            catch (Exception ex)
            {
                _services.GetRequiredService<ILogger<MainWindowViewModel>>().LogError(ex, "Backup reminder check failed");
            }
        }
    }
}