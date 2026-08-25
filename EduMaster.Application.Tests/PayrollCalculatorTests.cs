using EduMaster.Application.Payroll;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Payroll;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>
/// محرك الاحتساب (D-123/س-2): الصيغ الخمس بأمثلة عددية محسوبة يدوياً — علم الغائب غير المبرر (D-114: المبرر لا يُحسب أبداً) ·
/// أساس النسبة بأسعار متفاوتة (لقطة D-52 تُحترم) · التجاوز يعلو الافتراضية (D-113) · التقريب لأقرب سنتيم ·
/// لا سطر مبلغه صفر · بلا سياسة مغطية = تحذير لا إسقاط صامت · عزل الأساتذة بلقطة D-117.
/// </summary>
public sealed class PayrollCalculatorTests
{
    private static readonly DateTime T0 = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

    // ---------- بناة مساعدون (موضعية — مرآة استدعاء المستودع المُختبَر بناؤه) ----------
    private static PayPolicy TeacherPolicy(int id, PayPolicyKind kind, long rateCentimes, decimal? percentage = null, bool countsAbsent = false, int? classGroupId = null, int teacherId = 7)
        => PayPolicy.Load(id, PayeeKind.Teacher, teacherId, null, classGroupId, kind, rateCentimes, percentage, countsAbsent, true, T0, null, null, null);

    private static PayPolicy EmployeePolicy(int id, PayPolicyKind kind, long rateCentimes, int employeeId = 3)
        => PayPolicy.Load(id, PayeeKind.Employee, null, employeeId, null, kind, rateCentimes, null, false, true, T0, null, null, null);

    private static PayrollSessionFact Session(int id, int groupId, int teacherId, int minutes, string groupName = "فوج أ")
        => new(id, groupId, groupName, teacherId, T0, minutes);

    private static PayrollAttendanceFact Mark(int sessionId, AttendanceStatus status, long priceCentimes = 35000)
        => new(sessionId, status, priceCentimes);

    private static IEnumerable<PayrollAttendanceFact> Present(int sessionId, int count, long priceCentimes = 35000)
        => Enumerable.Range(0, count).Select(_ => Mark(sessionId, AttendanceStatus.Present, priceCentimes));

    private static readonly IReadOnlyDictionary<int, int> NoWorkDays = new Dictionary<int, int>();

    // ---------- لكل حاضر (D-114) ----------
    [Fact]
    public void PerPresentStudent_WithoutFlag_CountsPresentOnly()
    {
        var policies = new[] { TeacherPolicy(1, PayPolicyKind.PerPresentStudent, 20000) };   // 200.00 دج لكل محسوب
        var sessions = new[] { Session(100, 10, 7, 60), Session(101, 10, 7, 60) };
        var attendance = Present(100, 10)
            .Concat(new[] { Mark(100, AttendanceStatus.Absent), Mark(100, AttendanceStatus.Absent), Mark(100, AttendanceStatus.Justified) })
            .Concat(Present(101, 5))
            .Concat(new[] { Mark(101, AttendanceStatus.Absent), Mark(101, AttendanceStatus.Absent), Mark(101, AttendanceStatus.Absent) })
            .ToList();

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        var line = Assert.Single(result.Lines);
        Assert.Empty(result.Warnings);
        Assert.Equal(15m, line.Quantity);                    // الحاضرون فقط: 10 + 5
        Assert.Equal(300000L, line.AmountCentimes);          // 15 × 200.00 = 3000.00 دج
        Assert.Equal(1, line.PolicyId);
        Assert.Equal(PayeeKind.Teacher, line.PayeeKind);
        Assert.Contains("2 حصص", line.Details);
        Assert.Contains("15 محسوباً", line.Details);
    }

    [Fact]
    public void PerPresentStudent_WithFlag_CountsUnjustifiedAbsentToo()
    {
        var policies = new[] { TeacherPolicy(1, PayPolicyKind.PerPresentStudent, 20000, countsAbsent: true) };
        var sessions = new[] { Session(100, 10, 7, 60), Session(101, 10, 7, 60) };
        var attendance = Present(100, 10)
            .Concat(new[] { Mark(100, AttendanceStatus.Absent), Mark(100, AttendanceStatus.Absent), Mark(100, AttendanceStatus.Justified) })
            .Concat(Present(101, 5))
            .Concat(new[] { Mark(101, AttendanceStatus.Absent), Mark(101, AttendanceStatus.Absent), Mark(101, AttendanceStatus.Absent) })
            .ToList();

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        var line = Assert.Single(result.Lines);
        Assert.Equal(20m, line.Quantity);                    // 15 حاضراً + 5 غائب غير مبرر — المبرر مُستبعَد حتى مع العلم
        Assert.Equal(400000L, line.AmountCentimes);          // 20 × 200.00 = 4000.00 دج
    }

    // ---------- نسبة مئوية ----------
    [Fact]
    public void Percentage_UsesEachAttendeeAgreedPrice()
    {
        var policies = new[] { TeacherPolicy(2, PayPolicyKind.Percentage, 0, percentage: 60m) };
        var sessions = new[] { Session(100, 10, 7, 60) };
        var attendance = new[]
        {
            Mark(100, AttendanceStatus.Present, 35000),   // سعر كامل
            Mark(100, AttendanceStatus.Present, 30000),   // بخصم فردي — يُحترم (D-52)
            Mark(100, AttendanceStatus.Present, 25000),
            Mark(100, AttendanceStatus.Absent, 35000),    // غائب غير مبرر — خارج الأساس (العلم مُطفأ)
            Mark(100, AttendanceStatus.Justified, 35000), // مبرر — خارج الأساس دائماً
        };

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        var line = Assert.Single(result.Lines);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(54000L, line.AmountCentimes);        // 60% × (350+300+250 = 900.00) = 540.00 دج
        Assert.Equal(PayPolicyKind.Percentage, line.Kind);
        Assert.Equal(60m, line.Percentage.Value);
    }

    // ---------- بالساعة ----------
    [Fact]
    public void PerHour_ComputesFromTotalMinutes()
    {
        var policies = new[] { TeacherPolicy(3, PayPolicyKind.PerHour, 150000) };   // 1500.00 دج/ساعة
        var sessions = new[] { Session(100, 10, 7, 90), Session(101, 10, 7, 60), Session(102, 10, 7, 30) };

        var result = PayrollCalculator.Compute(policies, sessions, Array.Empty<PayrollAttendanceFact>(), NoWorkDays);

        var line = Assert.Single(result.Lines);
        Assert.Equal(3m, line.Quantity);                  // 180 دقيقة = 3 ساعات
        Assert.Equal(450000L, line.AmountCentimes);       // 3 × 1500.00 = 4500.00 دج
        Assert.Contains("ساعة", line.Details);
    }

    [Fact]
    public void PerHour_RoundsHalfCentimeAwayFromZero()
    {
        var policies = new[] { TeacherPolicy(3, PayPolicyKind.PerHour, 33333) };   // 333.33 دج/ساعة
        var sessions = new[] { Session(100, 10, 7, 30) };                          // نصف ساعة

        var result = PayrollCalculator.Compute(policies, sessions, Array.Empty<PayrollAttendanceFact>(), NoWorkDays);

        var line = Assert.Single(result.Lines);
        Assert.Equal(16667L, line.AmountCentimes);        // 33333 × 0.5 = 16666.5 ← نصف سنتيم للأعلى
        Assert.Equal(0.5m, line.Quantity);
    }

    // ---------- باليوم / شهري ثابت (موظفون) ----------
    [Fact]
    public void PerDay_MultipliesWorkDayCount()
    {
        var policies = new[] { EmployeePolicy(5, PayPolicyKind.PerDay, 80000) };   // 800.00 دج/يوم
        var workDays = new Dictionary<int, int> { [3] = 6 };

        var result = PayrollCalculator.Compute(policies, Array.Empty<PayrollSessionFact>(), Array.Empty<PayrollAttendanceFact>(), workDays);

        var line = Assert.Single(result.Lines);
        Assert.Equal(PayeeKind.Employee, line.PayeeKind);
        Assert.Equal(3, line.EmployeeId.GetValueOrDefault());
        Assert.Equal(6m, line.Quantity);
        Assert.Equal(480000L, line.AmountCentimes);       // 6 × 800.00 = 4800.00 دج
        Assert.Contains("6 أيام عمل", line.Details);
    }

    [Fact]
    public void PerDay_NoWorkDays_NoLine()
    {
        var policies = new[] { EmployeePolicy(5, PayPolicyKind.PerDay, 80000) };

        var result = PayrollCalculator.Compute(policies, Array.Empty<PayrollSessionFact>(), Array.Empty<PayrollAttendanceFact>(), NoWorkDays);

        Assert.Empty(result.Lines);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PerMonth_AlwaysFullAmount_EvenWithoutWorkDays()
    {
        var policies = new[] { EmployeePolicy(6, PayPolicyKind.PerMonth, 1200000) };   // 12 000.00 دج

        var result = PayrollCalculator.Compute(policies, Array.Empty<PayrollSessionFact>(), Array.Empty<PayrollAttendanceFact>(), NoWorkDays);

        var line = Assert.Single(result.Lines);
        Assert.Equal(1200000L, line.AmountCentimes);      // كاملاً — لا علاقة له بالأيام (D-113)
        Assert.Equal(1m, line.Quantity);
        Assert.Equal("شهري ثابت", line.Details);
    }

    // ---------- التجاوز يعلو الافتراضية (D-113) ----------
    [Fact]
    public void Override_WinsOverDefault_PerGroup()
    {
        var policies = new[]
        {
            TeacherPolicy(1, PayPolicyKind.PerPresentStudent, 20000),                        // افتراضية 200.00 دج
            TeacherPolicy(2, PayPolicyKind.PerPresentStudent, 25000, classGroupId: 20),      // تجاوز فوج «ب» 250.00 دج
        };
        var sessions = new[]
        {
            Session(100, 10, 7, 60),                               // فوج «أ» ← افتراضية
            Session(101, 20, 7, 60, groupName: "فوج ب"),           // فوج «ب» ← تجاوز
            Session(102, 20, 7, 60, groupName: "فوج ب"),
        };
        var attendance = Present(100, 10).Concat(Present(101, 4)).Concat(Present(102, 6)).ToList();

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Lines.Count);

        var defaultLine = result.Lines.Single(l => l.PolicyId == 1);
        Assert.Equal(200000L, defaultLine.AmountCentimes);         // فوج «أ» فقط: 10 × 200.00

        var overrideLine = result.Lines.Single(l => l.PolicyId == 2);
        Assert.Equal(250000L, overrideLine.AmountCentimes);        // (4+6) × 250.00
        Assert.Contains("فوج «فوج ب»", overrideLine.Details);      // نطاق التجاوز مُسمّى في التفصيل
    }

    // ---------- حواف ----------
    [Fact]
    public void SessionWithoutCoveringPolicy_ProducesWarning_NotLine()
    {
        // أستاذ بلا افتراضية — تجاوزه الوحيد على فوج «أ»، لكنه أقام حصة في فوج «ب»
        var policies = new[] { TeacherPolicy(2, PayPolicyKind.PerPresentStudent, 25000, classGroupId: 10) };
        var sessions = new[] { Session(100, 20, 7, 60, groupName: "فوج ب") };
        var attendance = Present(100, 12).ToList();

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        Assert.Empty(result.Lines);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(7, warning.TeacherId);
        Assert.Equal("فوج ب", warning.ClassGroupName);
        Assert.Equal(1, warning.SessionsCount);
    }

    [Fact]
    public void AllJustifiedAbsences_ZeroCounted_NoLine()
    {
        var policies = new[] { TeacherPolicy(1, PayPolicyKind.PerPresentStudent, 20000, countsAbsent: true) };
        var sessions = new[] { Session(100, 10, 7, 60) };
        var attendance = new[] { Mark(100, AttendanceStatus.Justified), Mark(100, AttendanceStatus.Justified) };

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        Assert.Empty(result.Lines);   // المبرر لا يُحسب حتى مع رفع العلم (D-114) — وسطر الصفر لا يُولَّد
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void TeacherWithPolicyButNoSessions_NoLine()
    {
        var policies = new[] { TeacherPolicy(1, PayPolicyKind.PerPresentStudent, 20000) };

        var result = PayrollCalculator.Compute(policies, Array.Empty<PayrollSessionFact>(), Array.Empty<PayrollAttendanceFact>(), NoWorkDays);

        Assert.Empty(result.Lines);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void TwoTeachers_SessionsIsolatedBySnapshotTeacher()
    {
        var policies = new[]
        {
            TeacherPolicy(1, PayPolicyKind.PerPresentStudent, 20000, teacherId: 7),
            TeacherPolicy(2, PayPolicyKind.PerPresentStudent, 20000, teacherId: 8),
        };
        var sessions = new[] { Session(100, 10, 7, 60), Session(101, 10, 8, 60) };   // لقطة D-117: النسبة لمن أقام فعلاً
        var attendance = Present(100, 10).Concat(Present(101, 5)).ToList();

        var result = PayrollCalculator.Compute(policies, sessions, attendance, NoWorkDays);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(200000L, result.Lines.Single(l => l.TeacherId == 7).AmountCentimes);   // 10 × 200.00
        Assert.Equal(100000L, result.Lines.Single(l => l.TeacherId == 8).AmountCentimes);   // 5 × 200.00
    }
}