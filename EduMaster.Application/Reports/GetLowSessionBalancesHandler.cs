using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Reports;

/// <summary>
/// تنبيه نفاد أرصدة الحصص (6.4 — ق-5): أرصدة النشطين الخام ← فلترة بالعتبة وترتيب تصاعدي (الأنفد أولاً) — السالب مسموح مرئي (D-92) ·
/// الفلترة والترتيب في الـHandler قصداً لتُختبرا عددياً بلا SQL · قراءة خالصة بلا معاملة وترمي الإلغاء (D-64)
/// </summary>
public sealed class GetLowSessionBalancesHandler
{
    /// <summary>العتبة الافتراضية 2 — عرف الشهر أربع حصص (D-91): نصف شهر متبقٍّ = وقت الاتصال للتجديد</summary>
    public const int DefaultThreshold = 2;

    private readonly IReportRepository _reports;
    private readonly ILogger<GetLowSessionBalancesHandler> _logger;

    public GetLowSessionBalancesHandler(IReportRepository reports, ILogger<GetLowSessionBalancesHandler> logger)
    {
        _reports = reports;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<LowSessionBalanceItem>>> ExecuteAsync(
        int? threshold = null, CancellationToken cancellationToken = default)
    {
        var limit = threshold ?? DefaultThreshold;
        if (limit < 0)
            return OperationResult<IReadOnlyList<LowSessionBalanceItem>>.Failure("عتبة التنبيه لا يمكن أن تكون سالبة.", ErrorType.Validation);

        try
        {
            var raw = await _reports.GetActiveEnrollmentBalancesAsync(cancellationToken);

            var items = raw
                .Select(r => new LowSessionBalanceItem(
                    r.EnrollmentId, r.StudentId, r.StudentName,
                    r.ClassGroupId, r.GroupName, r.SubjectName,
                    r.PurchasedSessions - r.ConsumedSessions,
                    string.IsNullOrWhiteSpace(r.GuardianName) ? null : r.GuardianName,
                    string.IsNullOrWhiteSpace(r.GuardianPhone) ? null : r.GuardianPhone,
                    string.IsNullOrWhiteSpace(r.StudentPhone) ? null : r.StudentPhone))
                .Where(i => i.Balance <= limit)
                .OrderBy(i => i.Balance)   // الأنفد أولاً — السالب (تجاوز) في المقدمة (D-92)
                .ThenBy(i => i.StudentName)
                .ToList();

            return OperationResult<IReadOnlyList<LowSessionBalanceItem>>.Success(items);
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build low session balances alert (threshold {Threshold})", limit);
            return OperationResult<IReadOnlyList<LowSessionBalanceItem>>.Failure("حدث خطأ غير متوقع أثناء إعداد تنبيه الأرصدة.", ErrorType.Unexpected);
        }
    }
}
