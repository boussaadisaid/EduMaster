using EduMaster.Application.Abstractions;
using EduMaster.UI.Academic;
using EduMaster.UI.AcademicYears;
using EduMaster.UI.ClassGroups;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Dialogs;
using EduMaster.UI.People;
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

            // ClassGroups (F2 — الشريحة 2.1)
            services.AddTransient<ClassGroupsViewModel>();
            services.AddTransient<ClassGroupEditorViewModel>();



            return services;
        }
    }
}
