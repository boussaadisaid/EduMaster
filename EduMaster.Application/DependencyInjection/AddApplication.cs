using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.AcademicYears.ActivateAcademicYear;
using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.Application.AcademicYears.DeactivateAcademicYear;
using EduMaster.Application.AcademicYears.SetCurrentAcademicYear;
using EduMaster.Application.AcademicYears.UpdateAcademicYear;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.People;
using EduMaster.Application.Pricing;
using EduMaster.Application.Students;
using EduMaster.Application.Teachers;
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
            services.AddTransient<GetAcademicYearByIdHandler>();   //  سطر التحقق — بدونه يبقى حقل الحقوق فارغاً
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
            services.AddTransient<SearchStudentsHandler>();
            services.AddTransient<CreateStudentHandler>();
            services.AddTransient<CreateStudentFileHandler>();
            services.AddTransient<UpdateStudentHandler>();
            services.AddTransient<SoftDeleteStudentHandler>();
            services.AddTransient<SetPersonPhotoHandler>();
            services.AddTransient<SearchTeachersHandler>();
            services.AddTransient<CreateTeacherHandler>();
            services.AddTransient<CreateTeacherFileHandler>();
            services.AddTransient<UpdateTeacherHandler>();
            services.AddTransient<SoftDeleteTeacherHandler>();
            services.AddTransient<GetLevelsHandler>();
            services.AddTransient<CreateLevelHandler>();
            services.AddTransient<UpdateLevelHandler>();
            services.AddTransient<DeactivateLevelHandler>();
            services.AddTransient<ActivateLevelHandler>();
            services.AddTransient<GetStreamsByLevelHandler>();
            services.AddTransient<CreateStreamHandler>();
            services.AddTransient<UpdateStreamHandler>();
            services.AddTransient<DeactivateStreamHandler>();
            services.AddTransient<ActivateStreamHandler>();
            services.AddTransient<GetSubjectsHandler>();
            services.AddTransient<CreateSubjectHandler>();
            services.AddTransient<UpdateSubjectHandler>();
            services.AddTransient<DeactivateSubjectHandler>();
            services.AddTransient<ActivateSubjectHandler>();
            services.AddTransient<GetRoomsHandler>();
            services.AddTransient<CreateRoomHandler>();
            services.AddTransient<UpdateRoomHandler>();
            services.AddTransient<DeactivateRoomHandler>();
            services.AddTransient<ActivateRoomHandler>();
            // ClassGroups (F2 — الشريحة 2.1)
            services.AddTransient<GetClassGroupsHandler>();
            services.AddTransient<GetClassGroupStreamIdsHandler>();
            services.AddTransient<CreateClassGroupHandler>();
            services.AddTransient<UpdateClassGroupHandler>();
            services.AddTransient<DeactivateClassGroupHandler>();
            services.AddTransient<ActivateClassGroupHandler>();
            // Pricing (F2 — الشريحة 2.2)
            services.AddTransient<GetSubjectPricesHandler>();
            services.AddTransient<CreateSubjectPriceHandler>();
            services.AddTransient<UpdateSubjectPriceHandler>();
            services.AddTransient<DeleteSubjectPriceHandler>();
            return services;
        }
    }
}
