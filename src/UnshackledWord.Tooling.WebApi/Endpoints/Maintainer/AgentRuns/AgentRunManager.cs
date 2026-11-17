using System.Collections.Concurrent;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

/// <summary>
/// Runs agent runs (roadmap 0016.6) in the background of this API process: the same maintainer-job idea as the seed jobs, but state lives in the
/// database (<c>AgentRuns</c>), so a status survives a restart. At startup it validates the provider profiles (a wrong configuration stops the API) and
/// marks runs that were running when the process stopped as <c>failed</c> (resumable). The loop itself is <see cref="AgentRunExecutor"/>.
/// </summary>
public sealed class AgentRunManager : IHostedService
{
    private readonly ConcurrentDictionary<int, Task> _active = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentRunManager> _logger;

    public AgentRunManager(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AgentRunManager> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsActive(int runId) => _active.ContainsKey(runId);

    /// <summary>Starts the executor for the run in its own scope and returns at once.</summary>
    public bool TryLaunch(int runId)
    {
        var gate = new TaskCompletionSource();
        if (!_active.TryAdd(runId, gate.Task))
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AgentRunExecutor>().RunAsync(runId, _shutdown.Token);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Agent run {RunId} ended with an unhandled error", runId);
            }
            finally
            {
                _active.TryRemove(runId, out _);
                gate.TrySetResult();
            }
        });

        return true;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var profiles = AgentProfiles.From(_configuration);
        var errors = profiles.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid AgentMapping configuration: " + string.Join(" ", errors));
        }

        foreach (var profile in profiles.Items.Where(p => !p.IsUsable))
        {
            _logger.LogWarning("Agent profile '{Profile}' ({Kind}) is configured but unusable: {Reason}", profile.Name, profile.Kind, profile.UnusableReason);
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var interrupted = await scope.ServiceProvider.GetRequiredService<IAgentRunRepository>().MarkInterruptedRunsAsync("Interrupted by an API restart; resume continues with the verses without a proposal.", cancellationToken);
            if (interrupted > 0)
            {
                _logger.LogWarning("{Count} agent run(s) were running at shutdown and are marked failed (resumable)", interrupted);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not mark interrupted agent runs (database not reachable yet?)");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _shutdown.CancelAsync();
        var running = _active.Values.ToArray();
        if (running.Length > 0)
        {
            await Task.WhenAny(Task.WhenAll(running), Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
        }
    }
}

public static class AgentRunMapper
{
    public static AgentRunDto ToDto(AgentRunRecord r, bool active) => new()
    {
        Id = r.Id,
        Profile = r.Profile,
        Model = r.Model,
        PromptVersion = r.PromptVersion,
        Book = r.BibleBookId,
        FromRefId = r.FromRefId,
        ToRefId = r.ToRefId,
        Mode = r.Mode,
        Status = r.Status,
        StatusNote = r.StatusNote,
        InputTokens = r.InputTokens,
        CachedInputTokens = r.CachedInputTokens,
        OutputTokens = r.OutputTokens,
        CostUsd = r.CostUsd,
        CacheRate = r.InputTokens == 0 ? 0 : Math.Round((double)r.CachedInputTokens / r.InputTokens, 3),
        EstimatedTokens = r.EstimatedTokens,
        EstimatedCostUsd = r.EstimatedCostUsd,
        MaxTokens = r.MaxTokens,
        MaxCostUsd = r.MaxCostUsd,
        CreatedAt = Utc(r.CreatedAt),
        StartedAt = r.StartedAt is { } s ? Utc(s) : null,
        FinishedAt = r.FinishedAt is { } f ? Utc(f) : null,
        Packages = r.Packages,
        PackagesCompleted = r.PackagesCompleted,
        PackagesFailed = r.PackagesFailed,
        ProposedVerses = r.ProposedVerses,
        FlaggedVerses = r.FlaggedVerses,
        OpenProposals = r.OpenProposals,
        Active = active,
    };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
