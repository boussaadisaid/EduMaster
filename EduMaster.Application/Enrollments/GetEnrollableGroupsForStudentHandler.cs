using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>الأفواج المؤهَّلة لطالب (D-83) — تغذي ديالوغ «إلحاق بفوج» الطالب-المحوري</summary>
public sealed class GetEnrollableGroupsForStudentHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly ILogger<GetEnrollableGroupsForStudentHandler> _logger;

    public GetEnrollableGroupsForStudentHandler(IClassGroupEnrollmentRepository groupEnrollments,
        ILogger<GetEnrollableGroupsForStudentHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ClassGroupListItem>>> ExecuteAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _groupEnrollments.GetEnrollableGroupsForStudentAsync(studentId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<ClassGroupListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Enrollable groups load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load enrollable groups for student {StudentId}", studentId);
            return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل الأفواج المؤهَّلة.", ErrorType.Unexpected);
        }
    }
}