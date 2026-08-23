namespace EduMaster.Domain.Enums;

/// <summary>حالة الحضور (D-93): الحاضر والغائب يخصمان من الرصيد · المبرر لا يخصم</summary>
public enum AttendanceStatus : byte
{
    Present = 1,
    Absent = 2,
    Justified = 3
}