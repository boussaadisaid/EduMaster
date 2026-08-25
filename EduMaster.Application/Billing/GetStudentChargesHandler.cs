using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Billing;

/// <summary>مستحقات طالب لقسم «المالية» — قراءة بلا معاملة</summary>
public sealed class GetStudentChargesHandler
{
    private readonly IChargeRepository _charges;
    private readonly ILogger<GetStudentChargesHandler> _logger;

    public GetStudentChargesHandler(IChargeRepository charges, ILogger<GetStudentChargesHandler> logger)
    {
        _charges = charges;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<StudentChargeItem>>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _charges.GetForStudentAsync(studentId, cancellationToken);
            return OperationResult<IReadOnlyList<StudentChargeItem>>.Success(items.ToList());
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load charges for student {StudentId}", studentId);
            return OperationResult<IReadOnlyList<StudentChargeItem>>.Failure("حدث خطأ غير متوقع أثناء تحميل المستحقات.", ErrorType.Unexpected);
        }
    }
}