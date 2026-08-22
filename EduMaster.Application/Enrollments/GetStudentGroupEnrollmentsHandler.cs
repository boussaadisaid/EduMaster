using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

public sealed class GetStudentGroupEnrollmentsHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly ILogger<GetStudentGroupEnrollmentsHandler> _logger;

    public GetStudentGroupEnrollmentsHandler(IClassGroupEnrollmentRepository groupEnrollments,
        ILogger<GetStudentGroupEnrollmentsHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<StudentGroupEnrollmentItem>>> ExecuteAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _groupEnrollments.GetForStudentAsync(studentId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<StudentGroupEnrollmentItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Student group enrollments load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load group enrollments for student {StudentId}", studentId);
            return OperationResult<IReadOnlyList<StudentGroupEnrollmentItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل أفواج الطالب.", ErrorType.Unexpected);
        }
    }
}