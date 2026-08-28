using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.AcademicYears.ActivateAcademicYear;
using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.Application.AcademicYears.DeactivateAcademicYear;
using EduMaster.Application.AcademicYears.SetCurrentAcademicYear;
using EduMaster.Application.AcademicYears.UpdateAcademicYear;
using EduMaster.Application.Backup;
using EduMaster.Application.Billing;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Employees;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Payroll;
using EduMaster.Application.People;
using EduMaster.Application.Pricing;
using EduMaster.Application.Printing;
using EduMaster.Application.Reports;
using EduMaster.Application.Scheduling;
using EduMaster.Application.Settings;
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
            services.AddTransient<GetAcademicYearByIdHandler>();
            services.AddTransient<CreateAcademicYearHandler>();
            services.AddTransient<UpdateAcademicYearHandler>();
            services.AddTransient<SetCurrentAcademicYearHandler>();
            services.AddTransient<DeactivateAcademicYearHandler>();
            services.AddTransient<ActivateAcademicYearHandler>();
            // People
            services.AddTransient<SearchPersonsHandler>();
            services.AddTransient<CreatePersonHandler>();
            services.AddTransient<FindPersonDuplicateHandler>();   // جديد 6.6-ب (ز-2) — سطر التحقق: بواحد
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
            // Pricing (F2 — الشريحتان 2.2/2.4)
            services.AddTransient<GetSubjectPricesHandler>();
            services.AddTransient<CreateSubjectPriceHandler>();
            services.AddTransient<UpdateSubjectPriceHandler>();
            services.AddTransient<DeleteSubjectPriceHandler>();
            services.AddTransient<GetSubjectPriceHandler>();
            // Enrollments (F2 — الشريحتان 2.3/2.4)
            services.AddTransient<GetAnnualEnrollmentsForStudentHandler>();
            services.AddTransient<RegisterAnnualEnrollmentHandler>();
            services.AddTransient<UpdateAnnualEnrollmentHandler>();
            services.AddTransient<WithdrawAnnualEnrollmentHandler>();
            services.AddTransient<GetClassGroupRosterHandler>();
            services.AddTransient<GetStudentGroupEnrollmentsHandler>();
            services.AddTransient<EnrollStudentInGroupHandler>();
            services.AddTransient<WithdrawGroupEnrollmentHandler>();
            services.AddTransient<TransferGroupEnrollmentHandler>();
            services.AddTransient<GetTransferTargetsHandler>();
            services.AddTransient<GetEnrollableGroupsForStudentHandler>();
            services.AddTransient<BulkRolloverHandler>();   // جديد 6.2-أ — الترحيل الجماعي (D-129)
            services.AddTransient<GetRolloverCandidatesHandler>();   // جديد 6.2-ج — قراءة المعاينة
            // Scheduling(F3 — الشرائح 3.1 / 3.2 / 3.3)   //  سطر التحقق: القسم كاملاً بستة عشر (6.6-ص-ب)
            services.AddTransient<GetTimetableHandler>();
            services.AddTransient<GetGroupSchedulesHandler>();
            services.AddTransient<GetSessionsHandler>();
            services.AddTransient<GetScheduleConflictsHandler>();
            services.AddTransient<CreateScheduleSlotHandler>();
            services.AddTransient<UpdateScheduleSlotHandler>();
            services.AddTransient<DeactivateScheduleSlotHandler>();
            services.AddTransient<ActivateScheduleSlotHandler>();
            services.AddTransient<GenerateSessionsHandler>();
            services.AddTransient<CreateAdHocSessionHandler>();
            services.AddTransient<CancelSessionHandler>();
            services.AddTransient<MarkSessionHeldHandler>();
            services.AddTransient<CorrectSessionTeacherHandler>();   // جديد 6.6-ص-ب
            services.AddTransient<PurchaseSessionsHandler>();
            services.AddTransient<GetSessionAttendanceHandler>();
            services.AddTransient<SaveSessionAttendanceHandler>();

            // Billing (F4 — الشرائح 4.1/4.2/4.3)   //  سطر التحقق: القسم بتسعة (6.6-ع-ب)
            services.AddTransient<GetStudentChargesHandler>();
            services.AddTransient<CancelChargeHandler>();
            services.AddTransient<ReduceChargeHandler>();
            services.AddTransient<GetPaymentContextHandler>();
            services.AddTransient<RegisterPaymentHandler>();
            services.AddTransient<GetDebtorsHandler>();
            services.AddTransient<GetPaymentsLogHandler>();
            services.AddTransient<RegisterRefundHandler>();
            services.AddTransient<ReverseReceiptHandler>();   // جديد 6.6-ع-ب (عكس قبض خاطئ — ع-4)

            // Payroll (F5 — الشريحة 5.1)   //  سطر التحقق: القسم باثني عشر
            services.AddTransient<GetEmployeesHandler>();
            services.AddTransient<CreateEmployeeHandler>();
            services.AddTransient<UpdateEmployeeHandler>();
            services.AddTransient<SoftDeleteEmployeeHandler>();
            services.AddTransient<CreateEmployeeFileHandler>();
            services.AddTransient<GetWorkLogHandler>();
            services.AddTransient<AddWorkLogDayHandler>();
            services.AddTransient<RemoveWorkLogDayHandler>();
            services.AddTransient<GetPayPoliciesHandler>();
            services.AddTransient<CreatePayPolicyHandler>();
            services.AddTransient<UpdatePayPolicyHandler>();
            services.AddTransient<SetPayPolicyActiveHandler>();
            // Payroll (F5 — الشريحة 5.2: الاحتساب والاعتماد)   //  سطر التحقق: القسم بتسعة — جديد 5.2-ج
            services.AddScoped<PayrollComputationService>();
            services.AddTransient<GeneratePayrollRunHandler>();
            services.AddTransient<RegeneratePayrollRunHandler>();
            services.AddTransient<ApprovePayrollRunHandler>();
            services.AddTransient<DeletePayrollRunHandler>();
            services.AddTransient<AddManualPayrollLineHandler>();
            services.AddTransient<RemoveManualPayrollLineHandler>();
            services.AddTransient<GetPayrollRunsHandler>();
            services.AddTransient<GetPayrollRunDetailsHandler>();

            // Payroll (F5 — الشريحة 5.3: الصرف والأرصدة)   //  سطر التحقق: القسم بثلاثة — جديد 5.3-ج
            services.AddTransient<RegisterPayoutHandler>();
            services.AddTransient<GetPayrollBalancesHandler>();
            services.AddTransient<GetPayeePayoutsHandler>();

            // Reports (F6 — الشريحة 6.1)   //  سطر التحقق: القسم باثنين — جديد 6.1-ب
            services.AddTransient<GetPaymentMovementReportHandler>();
            services.AddTransient<GetStudentStatementHandler>();

            // Settings (F6 — الشريحة 6.3: هوية المدرسة ط-7)   //  سطر التحقق: القسم بثلاثة — جديد 6.3-أ
            services.AddTransient<GetSchoolInfoHandler>();
            services.AddTransient<UpdateSchoolInfoHandler>();
            services.AddTransient<SetSchoolLogoHandler>();

            // Printing (F6 — الشريحة 6.3)   //  سطر التحقق: القسم بواحد — جديد 6.3-ج
            services.AddTransient<GetReceiptPrintModelHandler>();

            // Reports (F6 — الشريحة 6.4: الأكاديمية وملخصات الأجور)   //  سطر التحقق: القسم بثلاثة — جديد 6.4-أ
            services.AddTransient<GetAttendanceSummaryHandler>();
            services.AddTransient<GetGroupSessionsReportHandler>();
            services.AddTransient<GetLowSessionBalancesHandler>();

            // Backup (F6 — الشريحة 6.5: النسخ الاحتياطي ن-أ)   //  سطر التحقق: القسم بثلاثة — جديد 6.5-أ
            services.AddTransient<RunBackupHandler>();
            services.AddTransient<GetBackupStatusHandler>();
            services.AddTransient<SetBackupFolderHandler>();

            // Billing (F6 — الشريحة 6.6: استهلاك الزائدة الدائنة — ز-أ)   //  سطر التحقق: القسم بواحد — جديد 6.6-أ (Scoped كـPayrollComputationService — ينضم لمعاملة المتصل)
            services.AddScoped<CreditConsumptionService>();

            return services;
        }
    }
}
