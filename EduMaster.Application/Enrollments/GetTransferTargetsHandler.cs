using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

public sealed class GetTransferTargetsHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly ILogger<GetTransferTargetsHandler> _logger;

    public GetTransferTargetsHandler(IClassGroupEnrollmentRepository groupEnrollments,
        ILogger<GetTransferTargetsHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ClassGroupListItem>>> ExecuteAsync(
        int groupEnrollmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var current = await _groupEnrollments.GetByIdAsync(groupEnrollmentId, cancellationToken);
            if (current is null)
                return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure("التسجيل غير موجود.", ErrorType.NotFound);

            var items = (await _groupEnrollments.GetTransferTargetsAsync(groupEnrollmentId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ClassGroupListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Transfer targets load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load transfer targets for group enrollment {GroupEnrollmentId}", groupEnrollmentId);
            return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل أفواج النقل.", ErrorType.Unexpected);
        }
    }
}