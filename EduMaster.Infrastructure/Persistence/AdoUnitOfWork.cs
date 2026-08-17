using EduMaster.Application.Abstractions;
using System.Data;
using System.Data.Common;


namespace EduMaster.Infrastructure.Persistence
{
    public sealed class AdoUnitOfWork : IUnitOfWork, IAdoDbSession, IAsyncDisposable
    {
        private readonly DbConnection _connection;
        private DbTransaction? _transaction;

        public AdoUnitOfWork(DbConnection connection)
        {
            _connection = connection;
        }

        public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken ct = default)
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync(ct);

            return _connection;
        }

        public DbTransaction? CurrentTransaction => _transaction;

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {

            if (_transaction is not null)
                return;

            var connection = await GetOpenConnectionAsync(cancellationToken);
            _transaction = await connection.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is null)
                throw new InvalidOperationException("No active transaction.");

            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();

            _transaction = null;
        }

       

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is null)
                return;

            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();

            _transaction = null;
        }

        public async ValueTask DisposeAsync()
        {
            if(_transaction is not null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }

            await _connection.DisposeAsync();
        }
    }
}
