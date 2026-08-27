using EduMaster.Application.Reports;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>قراءات التقارير المالية والإيصالات والأكاديمية (F6) — خاماً بلا نصوص SQL مكررة (D-128)</summary>
public interface IReportRepository
{
    /// <summary>مدفوعات طالب كاملة مع كل تخصيصاتها دفعة واحدة — لكشف الحساب (6.1)</summary>
    Task<StudentPaymentsRead> GetPaymentsWithAllocationsForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>إيصال مفرد بتخصيصاته — للطباعة (6.3 — ط-2) · null إن لم يوجد</summary>
    Task<ReceiptPrintRead?> GetReceiptForPrintAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>علامات الحضور الخام في الحصص المُقامة لفترة (6.4 — ق-1) — التجميع في الـHandler (روح D-128) · الحدود [from, toExclusive) بتوقيت العمل المحلي (نمط قراءة الحصص القائمة)</summary>
    Task<IReadOnlyList<AttendanceMarkRaw>> GetAttendanceMarksForPeriodAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default);

    /// <summary>أرصدة التسجيلات النشطة في أفواج فعّالة خاماً (6.4 — ق-5) — الفلترة بالعتبة والترتيب في الـHandler (تُختبر عددياً بلا SQL)</summary>
    Task<IReadOnlyList<EnrollmentBalanceRaw>> GetActiveEnrollmentBalancesAsync(CancellationToken cancellationToken = default);
}
