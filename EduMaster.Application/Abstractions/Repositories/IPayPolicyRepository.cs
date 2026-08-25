using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;

namespace EduMaster.Application.Abstractions.Repositories;

public interface IPayPolicyRepository
{
    Task<PayPolicy?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PayPolicy?> GetActiveDefaultForTeacherAsync(int teacherId, CancellationToken cancellationToken = default);
    Task<PayPolicy?> GetActiveOverrideAsync(int teacherId, int classGroupId, CancellationToken cancellationToken = default);
    Task<PayPolicy?> GetActiveForEmployeeAsync(int employeeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayPolicyItem>> ListAsync(PayeeKind? payeeKind, int? payeeId, CancellationToken cancellationToken = default);
    Task AddAsync(PayPolicy policy, CancellationToken cancellationToken = default);
    Task UpdateAsync(PayPolicy policy, CancellationToken cancellationToken = default);
}