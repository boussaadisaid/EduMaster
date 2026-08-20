using EduMaster.UI.AcademicYears;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.People;
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



    }
}
