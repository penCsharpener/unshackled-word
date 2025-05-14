using UnshackledWord.Tooling.SeedDb.Services;

namespace UnshackledWord.Tooling.SeedDb;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;

    public Worker(IServiceScopeFactory scopeFactory, IHostApplicationLifetime lifetime)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var seedingService = scope.ServiceProvider.GetRequiredService<SeedingService>();

        await seedingService.SeedDatabaseAsync(stoppingToken);

        _lifetime.StopApplication();
    }
}
