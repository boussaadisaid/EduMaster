using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Infrastructure.Academic;
using EduMaster.Infrastructure.AcademicYears;
using EduMaster.Infrastructure.Backup;
using EduMaster.Infrastructure.Billing;
using EduMaster.Infrastructure.ClassGroups;
using EduMaster.Infrastructure.Employees;
using EduMaster.Infrastructure.Expenses;
using EduMaster.Infrastructure.Enrollments;
using EduMaster.Infrastructure.Files;
using EduMaster.Infrastructure.Payroll;
using EduMaster.Infrastructure.People;
using EduMaster.Infrastructure.Persistence;
using EduMaster.Infrastructure.Pricing;
using EduMaster.Infrastructure.Reports;
using EduMaster.Infrastructure.Scheduling;
using EduMaster.Infrastructure.Security;
using EduMaster.Infrastructure.Sms;
using EduMaster.Infrastructure.Settings;
using EduMaster.Infrastructure.Treasury;
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
            services.AddScoped<IPersonRepository, PersonRepository>();
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
            // ClassGroups (F2 — الشريحة 2.1)
            services.AddScoped<IClassGroupRepository, ClassGroupRepository>();
            // Pricing (F2 — الشريحة 2.2)
            services.AddScoped<ISubjectPriceRepository, SubjectPriceRepository>();
            // Enrollments (F2 — الشريحتان 2.3/2.4)
            services.AddScoped<IAnnualEnrollmentRepository, AnnualEnrollmentRepository>();
            services.AddScoped<IClassGroupEnrollmentRepository, ClassGroupEnrollmentRepository>();
            // Scheduling (F3 — الشرائح 3.1/3.2/3.3)   //  سطر التحقق
            services.AddScoped<IClassGroupScheduleRepository, ClassGroupScheduleRepository>();
            services.AddScoped<IClassSessionRepository, ClassSessionRepository>();
            services.AddScoped<IGroupSessionPurchaseRepository, GroupSessionPurchaseRepository>();
            services.AddScoped<ISessionBalanceRepository, SessionBalanceRepository>();
            services.AddScoped<IGroupSessionTransferRepository, GroupSessionTransferRepository>();
            services.AddScoped<ISessionAttendanceRepository, SessionAttendanceRepository>();

            // Billing (F4 — الشريحتان 4.1/4.2)   //  سطر التحقق
            services.AddScoped<IChargeRepository, ChargeRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();

            // Employees + Payroll (F5 — الشريحة 5.1)   //  سطر التحقق: القسم بثلاثة
            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<IEmployeeWorkLogRepository, EmployeeWorkLogRepository>();
            services.AddScoped<IPayPolicyRepository, PayPolicyRepository>();

            // Payroll (F5 — الشريحة 5.2: الاحتساب والاعتماد)   //   — جديد 5.2-ج
            services.AddScoped<IPayrollRunRepository, PayrollRunRepository>();
            services.AddScoped<IPayrollLineRepository, PayrollLineRepository>();
            services.AddScoped<IPayrollFactsRepository, PayrollFactsRepository>();

            // Payroll (F5 — الشريحة 5.3: الصرف)   //  سطر التحقق: القسم بواحد — جديد 5.3-ج
            services.AddScoped<IPayoutRepository, PayoutRepository>();

            // Expenses — المصاريف التشغيلية
            services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
            services.AddScoped<IExpenseRepository, ExpenseRepository>();

            // Treasury — الخزينة
            services.AddScoped<ITreasuryAccountRepository, TreasuryAccountRepository>();
            services.AddScoped<ITreasuryTransactionRepository, TreasuryTransactionRepository>();
            services.AddScoped<ITreasuryTransferRepository, TreasuryTransferRepository>();
            services.AddScoped<ITreasuryReadRepository, TreasuryReadRepository>();

            // SMS — TextBee + local encrypted configuration
            services.AddSingleton<ISmsSettingsStore, LocalSmsSettingsStore>();
            services.AddSingleton<ISmsProvider>(sp => new TextBeeSmsProvider(
                new HttpClient { BaseAddress = new Uri("https://api.textbee.dev/api/v1/") },
                sp.GetRequiredService<ISmsSettingsStore>()));
            services.AddScoped<ISmsTemplateRepository, SmsTemplateRepository>();
            services.AddScoped<ISmsRepository, SmsRepository>();

            // Reports (F6 — الشريحة 6.1)   //  سطر التحقق: القسم بواحد — جديد 6.1-ب
            services.AddScoped<IReportRepository, ReportRepository>();

            // Settings (F6 — الشريحة 6.3: هوية المدرسة ط-7)   //  سطر التحقق: القسم بواحد — جديد 6.3-أ
            services.AddScoped<ISchoolInfoRepository, SchoolInfoRepository>();

            // Backup (F6 — الشريحة 6.5: النسخ الاحتياطي ن-أ)   //  سطر التحقق: القسم باثنين
            services.AddScoped<IBackupGateway, SqlBackupGateway>();      // متصل بالقاعدة = Scoped
            services.AddSingleton<IBackupFileStore, BackupFileStore>();  // عديم الحالة (ملفات) = Singleton

            return services;
        }
    }
}
