using EduMaster.Application.Enrollments;
using EduMaster.Domain.Enrollments;


namespace EduMaster.Application.Abstractions.Repositories;

public interface IAnnualEnrollmentRepository
{
    Task AddAsync(AnnualEnrollment enrollment, CancellationToken cancellationToken = default);
    Task UpdateAsync(AnnualEnrollment enrollment, CancellationToken cancellationToken = default);
    Task<AnnualEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>فرادة النشط الودية: تسجيل نشط واحد لكل طالب في السنة — الفهرس المفلتر يحمي قاعدةً (D-53)</summary>
    Task<bool> AnyActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default);
    /// <summary>التسجيل السنوي النشط لطالب في سنة — الفهرس المفلتر يضمن صفاً واحداً كحد أقصى (يُستهلك في الإلحاق D-54)</summary>
    Task<AnnualEnrollment?> GetActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default);
    /// <summary>تسجيلات طالب (نشطة ومنسحبة) بأسماء السنة/المستوى/الشعبة — الأحدث أولاً</summary>
    Task<IEnumerable<AnnualEnrollmentListItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default);
    /// <summary>حارس D-54/D-72: تسجيلات فوج نشطة مرتبطة بهذا التسجيل (مفعَّل منذ 2.4)</summary>
    Task<bool> HasActiveGroupEnrollmentsAsync(int annualEnrollmentId, CancellationToken cancellationToken = default);

    /// <summary>مرشحو الترحيل الجماعي (6.2 — D-129): نشطو سنة المصدر بأسمائهم ومستوياتهم وديونهم + علم «في الهدف مسبقاً» + أهلية الشخص/الملف بسببها المرئي — قراءة مسطّحة بلا معاملة (D-40)</summary>
    Task<IReadOnlyList<RolloverCandidateItem>> GetRolloverCandidatesAsync(int sourceYearId, int targetYearId, CancellationToken cancellationToken = default);
}
