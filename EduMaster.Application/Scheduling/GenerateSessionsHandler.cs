using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>
/// التوليد من الجدول (D-87): يمر على المواعيد الفعّالة لأفواج فعّالة في الفترة وينشئ المجدولة —
/// آمن لإعادة الضغط (الموجود يُسقَط + الفرادة تحمي قاعدةً) · لا توليد لحصص انقضت · القيمة المرجعة = عدد المُنشأة
/// </summary>
public sealed record GenerateSessionsRequest(DateTime From, DateTime To, int? ClassGroupId);   // null = كل الأفواج

public sealed class GenerateSessionsHandler
{
    private readonly IClassGroupScheduleRepository _schedules;
    private readonly IClassSessionRepository _sessions;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateSessionsHandler> _logger;

    public GenerateSessionsHandler(IClassGroupScheduleRepository schedules, IClassSessionRepository sessions,
        IClock clock, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<GenerateSessionsHandler> logger)
    {
        _schedules = schedules;
        _sessions = sessions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(GenerateSessionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.To.Date < request.From.Date)
            return OperationResult<int>.Failure("تاريخ النهاية قبل تاريخ البداية.", ErrorType.Validation);

        var localToday = DateTime.Now.Date;   // StartsAt توقيت عمل محلي — ليس تدقيقاً
        if (request.From.Date < localToday)
            return OperationResult<int>.Failure("لا تُولَّد حصص في الماضي — ابدأ من اليوم فصاعداً.", ErrorType.Validation);

        if ((request.To.Date - request.From.Date).Days > 186)
            return OperationResult<int>.Failure("فترة التوليد طويلة جداً (الحد الأقصى 6 أشهر).", ErrorType.Validation);

        try
        {
            var slots = (await _schedules.GetActiveAsync(request.ClassGroupId, cancellationToken)).ToList();
            if (slots.Count == 0)
                return OperationResult<int>.Failure("لا مواعيد فعّالة في هذا النطاق (أو الفوج معطّل) — أضف موعداً من جدول استعمال الزمن أولاً.", ErrorType.BusinessRule);

            var utcNow = _clock.UtcNow;
            var localNow = DateTime.Now;
            var userId = _currentUser.UserAccountId;
            var from = request.From.Date;
            var toExclusive = request.To.Date.AddDays(1);
            var startsCache = new Dictionary<int, HashSet<DateTime>>();
            var created = 0;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            foreach (var slot in slots)
            {
                if (!startsCache.TryGetValue(slot.ClassGroupId, out var existingStarts))
                {
                    existingStarts = (await _sessions.GetSessionStartsAsync(slot.ClassGroupId, from, toExclusive, cancellationToken)).ToHashSet();
                    startsCache[slot.ClassGroupId] = existingStarts;
                }

                for (var day = from; day < toExclusive; day = day.AddDays(1))
                {
                    if (SchoolWeek.FromSystem(day.DayOfWeek) != slot.DayOfWeek)
                        continue;

                    var startsAt = day.Add(slot.StartTime.ToTimeSpan());
                    if (startsAt <= localNow)
                        continue;                        // لا توليد لحصة انقضت
                    if (!existingStarts.Add(startsAt))
                        continue;                        // موجودة مسبقاً — آمن لإعادة الضغط (D-87)

                    var session = Domain.Scheduling.ClassSession.Create(
                        slot.ClassGroupId, slot.Id, startsAt, slot.DurationMinutes, null, utcNow, userId);
                    await _sessions.AddAsync(session, cancellationToken);
                    created++;
                }
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            return OperationResult<int>.Success(created);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to generate sessions from {From} to {To} for class group {ClassGroupId}",
                request.From, request.To, request.ClassGroupId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء توليد الحصص.", ErrorType.Unexpected);
        }
    }
}