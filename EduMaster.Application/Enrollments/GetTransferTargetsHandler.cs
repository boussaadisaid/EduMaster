using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

public sealed class GetTransferTargetsHandler
{
    private readonly IClassGroupEnrollmentRepository _groupEnrollments;
    private readonly IClassGroupRepository _classGroups;
    private readonly IAcademicYearRepository _academicYears;
    private readonly ILogger<GetTransferTargetsHandler> _logger;

    public GetTransferTargetsHandler(IClassGroupEnrollmentRepository groupEnrollments, IClassGroupRepository classGroups,
        IAcademicYearRepository academicYears, ILogger<GetTransferTargetsHandler> logger)
    {
        _groupEnrollments = groupEnrollments;
        _classGroups = classGroups;
        _academicYears = academicYears;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ClassGroupListItem>>> ExecuteAsync(
        int groupEnrollmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentYear = await _academicYears.GetCurrentAcademicYearAsync(cancellationToken);
            if (currentYear is null)
                return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure("لا توجد سنة دراسية حالية مضبوطة.", ErrorType.BusinessRule);

            var current = await _groupEnrollments.GetByIdAsync(groupEnrollmentId, cancellationToken);
            if (current is null)
                return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure("التسجيل غير موجود.", ErrorType.NotFound);

            var group = await _classGroups.GetByIdAsync(current.ClassGroupId, cancellationToken);
            if (group is null)
                return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure("فوج التسجيل غير موجود.", ErrorType.NotFound);
            if (group.AcademicYearId != currentYear.Id)
                return OperationResult<IReadOnlyList<ClassGroupListItem>>.Failure("لا يمكن تحميل أهداف نقل لتسجيل من سنة دراسية سابقة أو غير حالية.", ErrorType.BusinessRule);

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