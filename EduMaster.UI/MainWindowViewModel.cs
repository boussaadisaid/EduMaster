using EduMaster.UI.Academic;
using EduMaster.UI.AcademicYears;
using EduMaster.UI.Billing;
using EduMaster.UI.ClassGroups;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Employees;
using EduMaster.UI.Payroll;
using EduMaster.UI.People;
using EduMaster.UI.Scheduling;
using EduMaster.UI.Students;
using EduMaster.UI.Teachers;
using Microsoft.Extensions.DependencyInjection;

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
                return Task.CompletedTask;
            });
            NavigateToAcademicYearsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<AcademicYearsViewModel>();   // نسخة طازجة = بيانات طازجة
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            NavigateToPeopleCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<PeopleViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            NavigateToStudentsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<StudentsViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            NavigateToTeachersCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<TeachersViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            // F5 — الشريحة 5.1: الموظفون (بند رئيسي — ب-1)
            NavigateToEmployeesCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<EmployeesViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            NavigateToClassGroupsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<ClassGroupsViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            // F3 — الشريحة 3.1: جدول استعمال الزمن + الحصص (بندان رئيسيان — عمل يومي لا إعداد)
            NavigateToTimetableCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<TimetableViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            NavigateToSessionsCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<SessionsViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            // F4 — الشريحة 4.3: المالية (ديون + سجل مدفوعات — عمل يومي)
            NavigateToFinanceCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<FinanceViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            // F5 — الشريحة 5.2: الأجور (الاحتساب والاعتماد — D-116)   // جديد هـ-2
            NavigateToPayrollCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<PayrollRunsViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            NavigateToAcademicStructureCommand = new AsyncRelayCommand(async () =>
            {
                var vm = _services.GetRequiredService<AcademicStructureViewModel>();
                CurrentViewModel = vm;
                await vm.InitializeAsync();
            });
            CurrentViewModel = _services.GetRequiredService<HomeViewModel>();      // الشاشة الافتتاحية
        }
        private object? _currentViewModel;
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
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
        public AsyncRelayCommand NavigateToPayrollCommand { get; }   // جديد هـ-2
        public AsyncRelayCommand NavigateToAcademicStructureCommand { get; }
    }
}