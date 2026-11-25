using System.Reflection;
using DbUp;

namespace UnshackledWord.Tooling.Postgres.Migration;

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
        var connectionString = configuration.GetConnectionString("PostgresConnection");
        var schema = "\"unshackled-word\"";

        var upgrader =
            DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                // Each script is atomic: a failure halfway through a destructive script (e.g. 0024) leaves the database untouched.
                .WithTransactionPerScript()
                // Default would be Npgsql's 30 s: data backfills such as 0047 (Greek editions, ~20 s on dev) must not be cut off on slower hosts.
                .WithExecutionTimeout(TimeSpan.FromMinutes(15))
                .LogTo(loggerFactory)
                .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            // Non-zero exit: compose starts SeedDb, WebApi and McpServer only after a successful migration.
            loggerFactory.CreateLogger<Worker>().LogCritical(result.Error, "Migration failed at script {Script}", result.ErrorScript?.Name);
            Environment.ExitCode = 1;
        }

        _applicationLifetime.StopApplication();
        return Task.CompletedTask;
    }
}
