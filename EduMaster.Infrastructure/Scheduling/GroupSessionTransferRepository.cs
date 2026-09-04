using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Scheduling;

public sealed class GroupSessionTransferRepository : IGroupSessionTransferRepository
{
    private readonly IAdoDbSession _session;

    public GroupSessionTransferRepository(IAdoDbSession session) => _session = session;

    public async Task AddAsync(Domain.Scheduling.GroupSessionTransfer transfer, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO GroupSessionTransfers
    (FromClassGroupEnrollmentId, ToClassGroupEnrollmentId, SessionsCount, TransferredAtUtc, Note, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES
    (@FromClassGroupEnrollmentId, @ToClassGroupEnrollmentId, @SessionsCount, @TransferredAtUtc, @Note, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                transfer.FromClassGroupEnrollmentId,
                transfer.ToClassGroupEnrollmentId,
                transfer.SessionsCount,
                transfer.TransferredAtUtc,
                transfer.Note,
                transfer.CreatedAtUtc,
                transfer.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        transfer.SetId(newId);
    }
}
