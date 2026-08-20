using EduMaster.Application.Abstractions;
using EduMaster.UI.AcademicYears;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Dialogs;
using EduMaster.UI.People;
using EduMaster.UI.Services;
using Microsoft.Extensions.DependencyInjection;



namespace EduMaster.UI.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddSingleton<CurrentUserService>();
            services.AddSingleton<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
            services.AddSingleton<MainWindow>();
            services.AddSingleton<LoginWindow>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddSingleton<IUserNotifier, ToastUserNotifier>();


            services.AddTransient<HomeViewModel>();
            services.AddTransient<AcademicYearsViewModel>();
            services.AddTransient<AcademicYearEditorViewModel>();
            services.AddTransient<ConfirmDialogViewModel>();
            services.AddTransient<DialogWindow>();
            services.AddSingleton<IDialogService, DialogService>();   // عديمة الحالة = Singleton (قواعد الوصفة)

            services.AddTransient<PeopleViewModel>();
            services.AddTransient<PersonEditorViewModel>();
            services.AddTransient<CreateAccountViewModel>();
            services.AddTransient<ResetPasswordViewModel>();
            services.AddTransient<ChangePasswordViewModel>();



            return services;
        }
    }
}
