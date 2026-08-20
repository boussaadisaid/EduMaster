using EduMaster.Application.AcademicYears;
using EduMaster.Application.AcademicYears.ActivateAcademicYear;
using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.Application.AcademicYears.DeactivateAcademicYear;
using EduMaster.Application.AcademicYears.SetCurrentAcademicYear;
using EduMaster.Application.AcademicYears.UpdateAcademicYear;
using EduMaster.Application.People;
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

            // People
            services.AddTransient<SearchPersonsHandler>();
            services.AddTransient<CreatePersonHandler>();
            services.AddTransient<UpdatePersonHandler>();
            services.AddTransient<DeactivatePersonHandler>();
            services.AddTransient<ActivatePersonHandler>();

            // Users / Accounts
            services.AddTransient<GetPersonAccountHandler>();
            services.AddTransient<CreateUserAccountHandler>();
            services.AddTransient<UnlockUserAccountHandler>();
            services.AddTransient<AdminResetPasswordHandler>();
            services.AddTransient<ChangePasswordHandler>();

            return services;
        }


    }
}
