using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Sms;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Sms;

public sealed class SmsTemplateRepository : ISmsTemplateRepository
{
    private readonly IAdoDbSession _session;
    public SmsTemplateRepository(IAdoDbSession session) => _session = session;
    private sealed record Row(int Id, string Name, byte Category, string Body, bool IsActive, DateTime CreatedAtUtc, int? CreatedByUserId, DateTime? UpdatedAtUtc, int? UpdatedByUserId);

    public async Task<SmsTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var r = await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(@"SELECT Id,Name,Category,Body,IsActive,CreatedAtUtc,CreatedByUserId,UpdatedAtUtc,UpdatedByUserId FROM dbo.SmsTemplates WHERE Id=@Id;", new { Id = id }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return r is null ? null : SmsTemplate.Load(r.Id, r.Name, (SmsMessageCategory)r.Category, r.Body, r.IsActive, r.CreatedAtUtc, r.CreatedByUserId, r.UpdatedAtUtc, r.UpdatedByUserId);
    }

    public async Task<IReadOnlyList<SmsTemplate>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<Row>(new CommandDefinition(@"SELECT Id,Name,Category,Body,IsActive,CreatedAtUtc,CreatedByUserId,UpdatedAtUtc,UpdatedByUserId FROM dbo.SmsTemplates WHERE (@ActiveOnly=0 OR IsActive=1) ORDER BY Name,Id;", new { ActiveOnly = activeOnly }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.Select(r => SmsTemplate.Load(r.Id, r.Name, (SmsMessageCategory)r.Category, r.Body, r.IsActive, r.CreatedAtUtc, r.CreatedByUserId, r.UpdatedAtUtc, r.UpdatedByUserId)).ToList();
    }

    public async Task<bool> AnyWithNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        return await c.ExecuteScalarAsync<bool>(new CommandDefinition(@"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.SmsTemplates WHERE Name=@Name AND (@ExcludeId IS NULL OR Id<>@ExcludeId)) THEN 1 ELSE 0 END;", new { Name = name.Trim(), ExcludeId = excludeId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(SmsTemplate template, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO dbo.SmsTemplates(Name,Category,Body,IsActive,CreatedAtUtc,CreatedByUserId) OUTPUT INSERTED.Id VALUES(@Name,@Category,@Body,1,@CreatedAtUtc,@CreatedByUserId);", new { template.Name, Category = (byte)template.Category, template.Body, template.CreatedAtUtc, template.CreatedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        template.SetId(id);
    }

    public async Task UpdateAsync(SmsTemplate template, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var affected = await c.ExecuteAsync(new CommandDefinition(@"UPDATE dbo.SmsTemplates SET Name=@Name,Category=@Category,Body=@Body,IsActive=@IsActive,UpdatedAtUtc=COALESCE(@UpdatedAtUtc,UpdatedAtUtc),UpdatedByUserId=@UpdatedByUserId WHERE Id=@Id;", new { template.Id, template.Name, Category = (byte)template.Category, template.Body, template.IsActive, template.UpdatedAtUtc, template.UpdatedByUserId }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException($"SmsTemplate {template.Id} was not found for update.");
    }
}
