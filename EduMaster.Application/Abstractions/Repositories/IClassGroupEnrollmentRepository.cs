using EduMaster.Application.ClassGroups;
using EduMaster.Application.Enrollments;
using EduMaster.Domain.Enrollments;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IClassGroupEnrollmentRepository
{
    Task AddAsync(ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default);
    Task<ClassGroupEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>فرادة النشط الودية: نشط واحد لكل طالب في الفوج — الفهرس المفلتر يحمي قاعدةً (D-53)</summary>
    Task<bool> AnyActiveForStudentInGroupAsync(int classGroupId, int studentId, CancellationToken cancellationToken = default);
    /// <summary>حارس السعة الصارمة (D-79): عدد النشطين في الفوج</summary>
    Task<int> CountActiveInGroupAsync(int classGroupId, CancellationToken cancellationToken = default);
    /// <summary>قائمة فوج (Roster) مسطّحة بأسماء الطلاب — النشطون أولاً</summary>
    Task<IEnumerable<ClassGroupEnrollmentListItem>> GetForGroupAsync(int classGroupId, CancellationToken cancellationToken = default);
    /// <summary>أفواج طالب (نشطة ومنسحبة) بأسماء الفوج/المادة/السنة — الأحدث أولاً</summary>
    Task<IEnumerable<StudentGroupEnrollmentItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default);
    /// <summary>التسجيلات النشطة المرتبطة بتسجيل سنوي — لكاسكيد الانسحاب السنوي (D-53)</summary>
    Task<IReadOnlyList<ClassGroupEnrollment>> GetActiveByAnnualEnrollmentIdAsync(int annualEnrollmentId, CancellationToken cancellationToken = default);
    /// <summary>أفواج النقل المطابقة (D-78): نفس سنة ومستوى التسجيل الحالي · فعّالة · غير ممتلئة · شعبة الطالب ضمن شعبها إن قُيّدت (D-59) · ليس مسجلاً فيها</summary>
    Task<IEnumerable<ClassGroupListItem>> GetTransferTargetsAsync(int groupEnrollmentId, CancellationToken cancellationToken = default);
    /// <summary>الأفواج المؤهَّلة لطالب (D-83): تطابق أي تسجيل سنوي نشط له (سنة ومستوى — وشعبة ضمن الشعب إن قُيّدت D-59) · فعّالة · غير ممتلئة · ليس مسجلاً فيها — تدعم تعدد السنوات النشطة (D-71)</summary>
    Task<IEnumerable<ClassGroupListItem>> GetEnrollableGroupsForStudentAsync(int studentId, CancellationToken cancellationToken = default);
}