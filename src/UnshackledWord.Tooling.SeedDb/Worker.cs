using UnshackledWord.Tooling.SeedDb.Services;

namespace UnshackledWord.Tooling.SeedDb;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory, IHostApplicationLifetime lifetime, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var seedingService = scope.ServiceProvider.GetRequiredService<SeedingService>();

        await seedingService.SeedDatabaseAsync(stoppingToken);

        _logger.LogInformation("Database seeding completed. Shutting down application.");
        //_lifetime.StopApplication();
    }
}
