using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

public sealed class GetClassGroupRosterHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly ILogger<GetClassGroupRosterHandler> _logger;

    public GetClassGroupRosterHandler(IClassGroupEnrollmentRepository groupEnrollments,
        ILogger<GetClassGroupRosterHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ClassGroupEnrollmentListItem>>> ExecuteAsync(
        int classGroupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _groupEnrollments.GetForGroupAsync(classGroupId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ClassGroupEnrollmentListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Class group roster load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load roster for class group {ClassGroupId}", classGroupId);
            return OperationResult<IReadOnlyList<ClassGroupEnrollmentListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل مسجَّلي الفوج.", ErrorType.Unexpected);
        }
    }
}