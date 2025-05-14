using System.Reflection;
using DbUp;

namespace UnshackledWord.Tooling.Sqlite.Migration;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public Worker(IServiceScopeFactory scopeFactory, IHostApplicationLifetime applicationLifetime)
    {
        _scopeFactory = scopeFactory;
        _applicationLifetime = applicationLifetime;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("SqliteConnection");

        var upgrader =
            DeployChanges.To
                .SqliteDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogTo(loggerFactory)
                .Build();
        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
        }

        _applicationLifetime.StopApplication();
        return Task.CompletedTask;
    }
}
