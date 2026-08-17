using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduMaster.Infrastructure.Persistence;


public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<OperationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // ⬅️ الـ Scope انتقل من الواجهة إلى هنا — بيته الصحيح
            await using var scope = _scopeFactory.CreateAsyncScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            // ⭐ القاعدة الجديدة: التهيئة "أفضل جهد" — تُسجَّل ولا تُسقط التطبيق أبداً
            _logger.LogError(ex, "Database initialization failed");
            return OperationResult.Failure("تعذّرت تهيئة قاعدة البيانات.", ErrorType.Unexpected);
        }
    }
}