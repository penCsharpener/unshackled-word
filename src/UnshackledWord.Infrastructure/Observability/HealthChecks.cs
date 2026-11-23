using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Observability;

/// <summary>Readiness of the Postgres connection: <c>SELECT 1</c> within two seconds.</summary>
public sealed class PostgresHealthCheck(IDbConnectionFactory connections, ILogger<PostgresHealthCheck> logger) : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);
        try
        {
            using var connection = await connections.CreateDbConnectionAsync(cts.Token);
            await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cts.Token));

            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Database health check failed");

            return HealthCheckResult.Unhealthy();
        }
    }
}

/// <summary>The highest applied migration script (DbUp journal) must reach the number this build expects.</summary>
public sealed partial class MigrationsHealthCheck(IDbConnectionFactory connections, ILogger<MigrationsHealthCheck> logger) : IHealthCheck
{
    /// <summary>Highest script number in <c>Tooling.Postgres.Migration/scripts</c> this build needs; a test keeps it in sync.</summary>
    public const int ExpectedMigration = 44;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PostgresHealthCheck.Timeout);
        try
        {
            using var connection = await connections.CreateDbConnectionAsync(cts.Token);
            // DbUp's default journal: table schemaversions, one row per applied script (name ends in "<number>_<name>.sql").
            var names = await connection.QueryAsync<string>(new CommandDefinition("SELECT scriptname FROM schemaversions", cancellationToken: cts.Token));

            return Evaluate(names, ExpectedMigration);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Migration health check failed");

            return HealthCheckResult.Unhealthy();
        }
    }

    public static HealthCheckResult Evaluate(IEnumerable<string> appliedScriptNames, int expected) =>
        HighestApplied(appliedScriptNames) >= expected ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();

    public static int HighestApplied(IEnumerable<string> scriptNames) =>
        scriptNames.Select(n => ScriptNumber().Match(n)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();

    [GeneratedRegex(@"(?:^|[./\\])(\d{4})_[^./\\]+\.sql$")]
    private static partial Regex ScriptNumber();
}

public static class HealthExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddUnshackledHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("database", tags: [ReadyTag])
            .AddCheck<MigrationsHealthCheck>("migrations", tags: [ReadyTag]);

        return services;
    }

    /// <summary>Maps <c>{prefix}/live</c> (process only, always 200) and <c>{prefix}/ready</c> (database + migrations, 503 when not), anonymous, JSON without exception text.</summary>
    public static IEndpointRouteBuilder MapUnshackledHealth(this IEndpointRouteBuilder endpoints, string prefix)
    {
        endpoints.MapHealthChecks($"{prefix}/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync }).AllowAnonymous();
        endpoints.MapHealthChecks($"{prefix}/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag), ResponseWriter = WriteAsync }).AllowAnonymous();

        return endpoints;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        });
    }
}
