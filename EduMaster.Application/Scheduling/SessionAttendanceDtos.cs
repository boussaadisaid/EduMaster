using EduMaster.Domain.Enums;

namespace EduMaster.Application.Scheduling;

/// <summary>علامة محفوظة — قراءة مسطّحة (D-40) لتركيب ديالوغ الحضور</summary>
public sealed record SessionAttendanceMarkItem(int ClassGroupEnrollmentId, AttendanceStatus Status, string? Note);

/// <summary>
/// صف ديالوغ الحضور: مسجَّل نشط (D-102) + حالته (المحفوظة أو الافتراضية حاضر — D-94) + ملاحظته.
/// IsPresentDefaultHint: لا علامة محفوظة بعد (الافتراضي ظاهر — D-94).
/// </summary>
public sealed record AttendanceRosterItem(
    int ClassGroupEnrollmentId,
    int StudentId,
    string FullName,
    AttendanceStatus Status,
    string? Note);

/// <summary>سطر حفظ واحد من الديالوغ</summary>
public sealed record SessionAttendanceEntry(int ClassGroupEnrollmentId, AttendanceStatus Status, string? Note);