using EduMaster.UI.AcademicYears;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.People;
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

    }
}
