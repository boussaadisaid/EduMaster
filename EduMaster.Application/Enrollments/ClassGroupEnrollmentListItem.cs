using EduMaster.Domain.Enums;

namespace EduMaster.Application.Enrollments;

/// <summary>صف قائمة الفوج (Roster) — نموذج قراءة مسطّح (D-40) · الأسعار بالسنتيم والعرض بالدينار عبر المحوّل (D-51)</summary>
public sealed record ClassGroupEnrollmentListItem(
    int Id,
    int StudentId,
    string FirstName,
    string LastName,
    string? FatherName,
    string? Phone,
    EnrollmentStatus Status,
    long SnapshotUnitPriceCentimes,
    long AgreedUnitPriceCentimes,
    string? DiscountNote,
    DateTime EnrolledAtUtc,
    DateTime? WithdrawnAtUtc)
{
    // الاسم ← اللقب ← اسم الأب (D-41)
    public string FullName => string.Join(" ", new[] { FirstName, LastName, FatherName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public string StatusText => Status == EnrollmentStatus.Active ? "نشط" : "منسحب";

    /// <summary>الفعلي يختلف عن سعر الجدول = خصم/اتفاق خاص (D-77)</summary>
    public bool HasDiscount => AgreedUnitPriceCentimes != SnapshotUnitPriceCentimes;
}