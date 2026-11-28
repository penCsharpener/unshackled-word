using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Seeding.Abstractions;

namespace UnshackledWord.Seeding.Catalog;

/// <summary>
/// Runs catalog steps one after the other (roadmap 0031.6): each in a fresh DI scope, on a connection string with the seed command timeout,
/// with a progress line per step, <see cref="ISeedDataChanged"/> after each, and stopping at the first failure (the exception ends the job).
/// The caller (the WebApi's seed job) has already planned the steps with <see cref="SeedRunPlanner"/>.
/// </summary>
public sealed class SeedStepRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IOptions<SeedingOptions> _options;
    private readonly SetupRunState _state;
    private readonly IEnumerable<ISeedDataChanged> _listeners;
    private readonly ILogger<SeedStepRunner> _logger;

    public SeedStepRunner(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<SeedingOptions> options,
        SetupRunState state,
        IEnumerable<ISeedDataChanged> listeners,
        ILogger<SeedStepRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _options = options;
        _state = state;
        _listeners = listeners;
        _logger = logger;
    }

    /// <summary>The connection string of seed jobs: the API's own with <c>Command Timeout</c> set (<c>Seeding:CommandTimeoutSeconds</c>, default 900).</summary>
    public static string BuildSeedConnectionString(string baseConnectionString, int commandTimeoutSeconds)
        => new NpgsqlConnectionStringBuilder(baseConnectionString) { CommandTimeout = commandTimeoutSeconds }.ConnectionString;

    public async Task<SeedJobResult> RunAsync(IReadOnlyList<string> keys, IProgress<string> progress, CancellationToken token, IReadOnlyList<SeedStep>? catalog = null)
    {
        catalog ??= SeedCatalog.Steps;
        var result = new SeedJobResult();
        var baseConnection = _configuration.GetConnectionString("PostgresConnection");
        var timeout = _options.Value.CommandTimeoutSeconds > 0 ? _options.Value.CommandTimeoutSeconds : SeedingOptions.DefaultCommandTimeoutSeconds;
        using var _ = baseConnection is null ? null : SeedConnectionContext.Use(BuildSeedConnectionString(baseConnection, timeout));

        progress.Report($"Plan: {keys.Count} step(s): {string.Join(", ", keys)}");
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var label = $"[{i + 1}/{keys.Count}] {key}";
            var step = catalog.FirstOrDefault(c => c.Key == key);
            if (step?.Runner is null)
            {
                _state.MarkFailed(key);
                throw new InvalidOperationException($"Step '{key}' has no runner in the seeding library.");
            }

            progress.Report($"{label}: start");
            _state.CurrentStep = key;
            var watch = Stopwatch.StartNew();
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var runner = (IRunner)scope.ServiceProvider.GetRequiredService(step.Runner);
                await runner.Run(token);
            }
            catch (Exception ex)
            {
                _state.CurrentStep = null;
                _state.MarkFailed(key);
                progress.Report($"{label}: failed after {watch.Elapsed.TotalSeconds:0.0} s ({ex.GetType().Name}: {ex.Message})");
                throw;
            }

            _state.MarkCompleted(key);
            progress.Report($"{label}: done in {watch.Elapsed.TotalSeconds:0.0} s");
            result.Steps.Add(await ReportAsync(step, watch.Elapsed, token));
            await NotifyAsync(key, token);
        }

        _state.CurrentStep = null;
        return result;
    }

    private async Task<SeedStepReport> ReportAsync(SeedStep step, TimeSpan elapsed, CancellationToken token)
    {
        var report = new SeedStepReport { Kind = step.Key, Action = "run" };
        report.Counts["seconds"] = (int)Math.Round(elapsed.TotalSeconds);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var counts = await scope.ServiceProvider.GetRequiredService<SetupStatusService>().CountTablesAsync([step], token);
            foreach (var table in step.Tables)
            {
                if (counts.TryGetValue(table.Name, out var c))
                {
                    report.Counts[table.Name] = (int)Math.Min(c.Rows, int.MaxValue);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not count the tables of setup step {Step}", step.Key);
        }

        return report;
    }

    private async Task NotifyAsync(string key, CancellationToken token)
    {
        foreach (var listener in _listeners)
        {
            try
            {
                await listener.SeedDataChangedAsync(key, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Listener} failed to reload after setup step {Step}", listener.GetType().Name, key);
            }
        }
    }
}
