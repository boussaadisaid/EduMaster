using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Scheduling;

/// <summary>append-only (D-91) — كتابة فقط؛ مجموعات الرصيد تُحسب في الاستعلامات المسطّحة مباشرة</summary>
public sealed class GroupSessionPurchaseRepository : IGroupSessionPurchaseRepository
{
    private readonly IAdoDbSession _session;

    public GroupSessionPurchaseRepository(IAdoDbSession session) => _session = session;

    public async Task AddAsync(Domain.Scheduling.GroupSessionPurchase purchase, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO GroupSessionPurchases (ClassGroupEnrollmentId, SessionsCount, PurchasedAtUtc, Note, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@ClassGroupEnrollmentId, @SessionsCount, @PurchasedAtUtc, @Note, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                purchase.ClassGroupEnrollmentId,
                purchase.SessionsCount,
                purchase.PurchasedAtUtc,
                purchase.Note,
                purchase.CreatedAtUtc,
                purchase.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        purchase.SetId(newId);
    }
}