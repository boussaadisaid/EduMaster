using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Scheduling;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Scheduling;

public sealed class SessionBalanceRepository : ISessionBalanceRepository
{
    private readonly IAdoDbSession _session;

    public SessionBalanceRepository(IAdoDbSession session) => _session = session;

    public async Task<SessionBalanceSnapshot?> GetAsync(int classGroupEnrollmentId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT
    PurchasedSessions = (SELECT ISNULL(SUM(p.SessionsCount), 0)
                         FROM GroupSessionPurchases p
                         WHERE p.ClassGroupEnrollmentId = @Id),
    TransferredInSessions = (SELECT ISNULL(SUM(t.SessionsCount), 0)
                             FROM GroupSessionTransfers t
                             WHERE t.ToClassGroupEnrollmentId = @Id),
    TransferredOutSessions = (SELECT ISNULL(SUM(t.SessionsCount), 0)
                              FROM GroupSessionTransfers t
                              WHERE t.FromClassGroupEnrollmentId = @Id),
    ConsumedSessions = (SELECT COUNT(*)
                        FROM SessionAttendance sa
                        WHERE sa.ClassGroupEnrollmentId = @Id
                          AND sa.Status IN (1, 2))
FROM ClassGroupEnrollments e WITH (UPDLOCK, HOLDLOCK)
WHERE e.Id = @Id;";

        var row = await connection.QuerySingleOrDefaultAsync<SessionBalanceSnapshot>(
            new CommandDefinition(sql, new { Id = classGroupEnrollmentId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row;
    }
}
