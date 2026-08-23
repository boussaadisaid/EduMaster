using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Scheduling;

/// <summary>حصص فترة (D-94: شاشة الحصص — فلتر تاريخ + فوج) — النهاية نهارية شاملة (تُحوَّل حصرياً داخلياً)</summary>
public sealed class GetSessionsHandler
{
    private readonly IClassSessionRepository _sessions;
    private readonly ILogger<GetSessionsHandler> _logger;

    public GetSessionsHandler(IClassSessionRepository sessions, ILogger<GetSessionsHandler> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ClassSessionListItem>>> ExecuteAsync(
        DateTime from, DateTime to, int? classGroupId, CancellationToken cancellationToken = default)
    {
        if (to.Date < from.Date)
            return OperationResult<IReadOnlyList<ClassSessionListItem>>.Failure("تاريخ النهاية قبل تاريخ البداية.", ErrorType.Validation);

        try
        {
            var items = (await _sessions.GetByDateRangeAsync(from.Date, to.Date.AddDays(1), classGroupId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ClassSessionListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Sessions load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sessions from {From} to {To} for class group {ClassGroupId}", from, to, classGroupId);
            return OperationResult<IReadOnlyList<ClassSessionListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل الحصص.", ErrorType.Unexpected);
        }
    }
}