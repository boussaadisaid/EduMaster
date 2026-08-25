using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IEmployeeWorkLogRepository
{
    Task AddAsync(WorkLogEntry entry, CancellationToken cancellationToken = default);
    /// <summary>حذف يوم بالمعرف — التصحيح الوحيد لسجل «كتابة فقط»: حذف اليوم وإعادة تسجيله · تعيد عدد الصفوف المحذوفة (نمط DeleteForSessionAsync)</summary>
    Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>أيام عمل موظف مرتبة زمنياً بنطاق اختياري (للعرض الآن، ولاحتساب «باليوم» في 5.2) · WorkDate تُقرأ DateTime من عمود DATE عند الحدود (D-112)</summary>
    Task<IReadOnlyList<WorkLogItem>> GetForEmployeeAsync(int employeeId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}