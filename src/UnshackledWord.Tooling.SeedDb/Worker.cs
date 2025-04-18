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

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for a while before checking again
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            // Check if the application is stopped
            if (stoppingToken.IsCancellationRequested)
            {
                // If the application is stopped, we can exit gracefully.
                _lifetime.StopApplication();
                return;
            }
        }
    }
}
