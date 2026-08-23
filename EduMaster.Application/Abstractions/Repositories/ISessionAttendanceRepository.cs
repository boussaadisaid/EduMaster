using EduMaster.Application.Scheduling;
using EduMaster.Domain.Scheduling;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>كتابة بالاستبدال الذرّي (D-101) + قراءة مسطّحة للعلامات (D-40) — لا قراءة كيانية</summary>
public interface ISessionAttendanceRepository
{
    Task AddAsync(SessionAttendance attendance, CancellationToken cancellationToken = default);

    /// <summary>يحذف كل علامات الحصة — أول خطوة في الاستبدال الذرّي (داخل معاملة الحافظ) · يعيد عدد المحذوف</summary>
    Task<int> DeleteForSessionAsync(int classSessionId, CancellationToken cancellationToken = default);

    /// <summary>العلامات المحفوظة لحصة — مسطّحة (التسجيل ← حالته وملاحظته)</summary>
    Task<IEnumerable<SessionAttendanceMarkItem>> GetMarksForSessionAsync(int classSessionId, CancellationToken cancellationToken = default);
}