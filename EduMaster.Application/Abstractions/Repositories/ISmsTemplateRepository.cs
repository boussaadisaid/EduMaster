using EduMaster.Domain.Sms;

namespace EduMaster.Application.Abstractions.Repositories;

public interface ISmsTemplateRepository
{
    Task<SmsTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmsTemplate>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(SmsTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(SmsTemplate template, CancellationToken cancellationToken = default);
}
