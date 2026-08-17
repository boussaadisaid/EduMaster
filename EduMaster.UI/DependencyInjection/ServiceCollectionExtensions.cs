using EduMaster.Application.Abstractions;
using EduMaster.UI.Common.Services;
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



            return services;
        }
    }
}
