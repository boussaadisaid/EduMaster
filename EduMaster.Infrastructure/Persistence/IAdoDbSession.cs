using System.Data.Common;


namespace EduMaster.Infrastructure.Persistence
{
    public interface IAdoDbSession
    {
        Task<DbConnection> GetOpenConnectionAsync(CancellationToken ct = default);
        DbTransaction? CurrentTransaction { get; }
    }
}
