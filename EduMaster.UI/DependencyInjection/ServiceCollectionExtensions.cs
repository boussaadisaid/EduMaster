using EduMaster.Application.Abstractions;
using EduMaster.UI.Academic;
using EduMaster.UI.AcademicYears;
using EduMaster.UI.Billing;
using EduMaster.UI.ClassGroups;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Dialogs;
using EduMaster.UI.Employees;
using EduMaster.UI.Enrollments;
using EduMaster.UI.Payroll;
using EduMaster.UI.People;
using EduMaster.UI.Pricing;
using EduMaster.UI.Printing;
using EduMaster.UI.Reports;
using EduMaster.UI.Scheduling;
using EduMaster.UI.Services;
using EduMaster.UI.Students;
using EduMaster.UI.Teachers;
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
            services.AddTransient<StudentsViewModel>();
            services.AddTransient<StudentEditorViewModel>();
            services.AddTransient<TeachersViewModel>();
            services.AddTransient<TeacherEditorViewModel>();
            services.AddTransient<AssignStudentRoleViewModel>();
            services.AddTransient<AssignTeacherRoleViewModel>();
            services.AddTransient<AcademicStructureViewModel>();
            services.AddTransient<LevelEditorViewModel>();
            services.AddTransient<StreamEditorViewModel>();
            services.AddTransient<SubjectEditorViewModel>();
            services.AddTransient<RoomEditorViewModel>();
            // ClassGroups (F2 — الشريحتان 2.1/2.4)
            services.AddTransient<ClassGroupsViewModel>();
            services.AddTransient<ClassGroupEditorViewModel>();
            services.AddTransient<ClassGroupRosterDialogViewModel>();
            services.AddTransient<TransferGroupEnrollmentViewModel>();
            // Pricing (F2 — الشريحة 2.2)
            services.AddTransient<SubjectPriceEditorViewModel>();
            // Enrollments (F2 — الشريحتان 2.3/2.4)
            services.AddTransient<AnnualEnrollmentEditorViewModel>();
            services.AddTransient<EnrollInGroupDialogViewModel>();
            services.AddTransient<RolloverDialogViewModel>();
            // Scheduling
            services.AddTransient<TimetableViewModel>();
            services.AddTransient<SessionsViewModel>();
            services.AddTransient<ScheduleSlotEditorViewModel>();
            services.AddTransient<GenerateSessionsDialogViewModel>();
            services.AddTransient<AdHocSessionViewModel>();
            services.AddTransient<PurchaseSessionsDialogViewModel>();
            services.AddTransient<SessionAttendanceDialogViewModel>();
            // Billing (F4 — الشرائح 4.1/4.2/4.3)   //  سطر التحقق: القسم بأربعة
            services.AddTransient<ChargeSettlementDialogViewModel>();
            services.AddTransient<PaymentDialogViewModel>();
            services.AddTransient<FinanceViewModel>();
            services.AddTransient<RefundDialogViewModel>();
            // Employees (F5 — دفعة B-1)   //  سطر التحقق: القسم بثلاثة
            services.AddTransient<EmployeesViewModel>();
            services.AddTransient<EmployeeEditorViewModel>();
            services.AddTransient<AssignEmployeeRoleViewModel>();
            // Payroll (F5 — دفعة B-2)   //  سطر التحقق: القسم بواحد
            services.AddTransient<PayPolicyDialogViewModel>();

            services.AddTransient<PayrollRunsViewModel>();

            services.AddTransient<PayoutDialogViewModel>();

            // Reports (F6 — الشريحة 6.1)   //  سطر التحقق: القسم بثلاثة
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<PaymentMovementReportViewModel>();
            services.AddTransient<StudentStatementViewModel>();
            // Reports (F6 — الشريحة 6.4: ق-ب)   //  سطر التحقق: القسم بثلاثة
            services.AddTransient<AttendanceSummaryReportViewModel>();
            services.AddTransient<GroupSessionsReportViewModel>();
            services.AddTransient<LowSessionBalancesViewModel>();
            // Printing (F6 — الشريحة 6.3: ط-د)   //  سطر التحقق: القسم بواحد
            services.AddSingleton<IPrintService, PrintService>();   // عديمة الحالة = Singleton — نافذة الطباعة تُنشأ لكل عملية (قواعد الوصفة)
            return services;
        }
    }
}
