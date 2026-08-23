namespace EduMaster.Domain.Common;

/// <summary>أسبوع المدرسة (D-86): 1=السبت … 7=الجمعة — تخزين وعرض وترتيب بهذا الترقيم، والتحويل من/إلى System.DayOfWeek هنا وحده</summary>
public static class SchoolWeek
{
    public static int FromSystem(DayOfWeek day) => day switch
    {
        DayOfWeek.Saturday => 1,
        DayOfWeek.Sunday => 2,
        DayOfWeek.Monday => 3,
        DayOfWeek.Tuesday => 4,
        DayOfWeek.Wednesday => 5,
        DayOfWeek.Thursday => 6,
        DayOfWeek.Friday => 7,
        _ => throw new DomainException("يوم أسبوع غير معروف.")
    };

    public static DayOfWeek ToSystem(int schoolDay) => schoolDay switch
    {
        1 => DayOfWeek.Saturday,
        2 => DayOfWeek.Sunday,
        3 => DayOfWeek.Monday,
        4 => DayOfWeek.Tuesday,
        5 => DayOfWeek.Wednesday,
        6 => DayOfWeek.Thursday,
        7 => DayOfWeek.Friday,
        _ => throw new DomainException("يوم الأسبوع يجب أن يكون بين 1 (السبت) و7 (الجمعة).")
    };

    public static string ArabicName(int schoolDay) => schoolDay switch
    {
        1 => "السبت",
        2 => "الأحد",
        3 => "الاثنين",
        4 => "الثلاثاء",
        5 => "الأربعاء",
        6 => "الخميس",
        7 => "الجمعة",
        _ => "؟"
    };
}