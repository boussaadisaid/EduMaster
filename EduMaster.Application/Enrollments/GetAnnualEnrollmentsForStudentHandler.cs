using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

public sealed class GetAnnualEnrollmentsForStudentHandler
{
    private readonly IAnnualEnrollmentRepository _enrollments;
    private readonly ILogger<GetAnnualEnrollmentsForStudentHandler> _logger;

    public GetAnnualEnrollmentsForStudentHandler(IAnnualEnrollmentRepository enrollments,
        ILogger<GetAnnualEnrollmentsForStudentHandler> logger)
    {
        _enrollments = enrollments;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<AnnualEnrollmentListItem>>> ExecuteAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _enrollments.GetForStudentAsync(studentId, cancellationToken)).ToList();
            return OperationResult<IReadOnlyList<AnnualEnrollmentListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: الإلغاء ليس خطأ
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Annual enrollments load cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load annual enrollments for student {StudentId}", studentId);
            return OperationResult<IReadOnlyList<AnnualEnrollmentListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء تحميل التسجيلات.", ErrorType.Unexpected);
        }
    }
}