using EduMaster.Application.Users;
using Microsoft.Extensions.DependencyInjection;


namespace EduMaster.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddTransient<LoginHandler>();

            return services;
        }


    }
}
