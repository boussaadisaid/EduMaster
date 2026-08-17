using EduMaster.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace EduMaster.Infrastructure.Persistence;

public sealed class DatabaseInitializationHostedService : BackgroundService
{
    private readonly IDatabaseInitializer _initializer;

    public DatabaseInitializationHostedService(IDatabaseInitializer initializer)
    {
        _initializer = initializer;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _initializer.InitializeAsync(stoppingToken);
}