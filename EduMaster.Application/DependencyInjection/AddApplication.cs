using EduMaster.Application.AcademicYears;
using EduMaster.Application.AcademicYears.ActivateAcademicYear;
using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.Application.AcademicYears.DeactivateAcademicYear;
using EduMaster.Application.AcademicYears.SetCurrentAcademicYear;
using EduMaster.Application.AcademicYears.UpdateAcademicYear;
using EduMaster.Application.Users;
using Microsoft.Extensions.DependencyInjection;


namespace EduMaster.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddTransient<LoginHandler>();
            services.AddTransient<GetAllAcademicYearsHandler>();
            services.AddTransient<CreateAcademicYearHandler>();
            services.AddTransient<UpdateAcademicYearHandler>();
            services.AddTransient<SetCurrentAcademicYearHandler>();
            services.AddTransient<DeactivateAcademicYearHandler>();
            services.AddTransient<ActivateAcademicYearHandler>();

            return services;
        }


    }
}
