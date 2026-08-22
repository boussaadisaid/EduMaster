using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Infrastructure.Academic;
using EduMaster.Infrastructure.AcademicYears;
using EduMaster.Infrastructure.ClassGroups;
using EduMaster.Infrastructure.Files;
using EduMaster.Infrastructure.People;
using EduMaster.Infrastructure.Persistence;
using EduMaster.Infrastructure.Pricing;
using EduMaster.Infrastructure.Security;
using EduMaster.Infrastructure.Students;
using EduMaster.Infrastructure.Teachers;
using EduMaster.Infrastructure.Time;
using EduMaster.Infrastructure.Users;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;



namespace EduMaster.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            string connectionString)
        {

            services.AddScoped<DbConnection>(_ => new SqlConnection(connectionString));
            services.AddScoped<AdoUnitOfWork>();
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AdoUnitOfWork>());
            services.AddScoped<IAdoDbSession>(sp => sp.GetRequiredService<AdoUnitOfWork>());
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<IDatabaseHealthCheck>(sp =>
                new DatabaseHealthCheck(connectionString, sp.GetRequiredService<ILogger<DatabaseHealthCheck>>()));

            services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
            services.AddScoped<IUserAccountRepository, UserAccountRepository>();
            services.AddScoped<IPersonRepository,PersonRepository>();
            services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
            services.AddTransient<DatabaseSeeder>();
            services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
            services.AddHostedService<DatabaseInitializationHostedService>();

            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<ITeacherRepository, TeacherRepository>();
            services.AddSingleton<IImageStore, ImageStore>();

            services.AddScoped<ILevelRepository, LevelRepository>();
            services.AddScoped<IStreamRepository, StreamRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();

            services.AddScoped<IClassGroupRepository, ClassGroupRepository>();

            // Pricing (F2 — الشريحة 2.2)
            services.AddScoped<ISubjectPriceRepository, SubjectPriceRepository>();

            return services;
        }
    }
}
