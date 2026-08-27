using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>مرشحو الترحيل الجماعي لمعاينة 6.2 (D-129/تر-5/تر-6) — قراءة خالصة بلا معاملة، ترمي الإلغاء (D-64)</summary>
public sealed class GetRolloverCandidatesHandler
{
    private readonly IAnnualEnrollmentRepository _enrollments;
    private readonly ILogger<GetRolloverCandidatesHandler> _logger;

    public GetRolloverCandidatesHandler(IAnnualEnrollmentRepository enrollments, ILogger<GetRolloverCandidatesHandler> logger)
    {
        _enrollments = enrollments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<RolloverCandidateItem>>> ExecuteAsync(int sourceYearId, int targetYearId, CancellationToken cancellationToken = default)
    {
        if (sourceYearId == targetYearId)
            return OperationResult<IReadOnlyList<RolloverCandidateItem>>.Failure("سنة المصدر هي نفسها سنة الهدف — اختر سنتين مختلفتين.", ErrorType.Validation);

        try
        {
            var items = await _enrollments.GetRolloverCandidatesAsync(sourceYearId, targetYearId, cancellationToken);
            return OperationResult<IReadOnlyList<RolloverCandidateItem>>.Success(items);
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rollover candidates from year {SourceYearId} to year {TargetYearId}", sourceYearId, targetYearId);
            return OperationResult<IReadOnlyList<RolloverCandidateItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل مرشحي الترحيل.", ErrorType.Unexpected);
        }
    }
}