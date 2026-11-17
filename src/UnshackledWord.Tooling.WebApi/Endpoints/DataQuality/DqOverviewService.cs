using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>Runs the single background refresh and tells whether one is running (one API instance; the flag is process-wide).</summary>
public sealed class DqRefreshCoordinator : IStatsRefreshStatus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DqRefreshCoordinator> _logger;
    private int _running;

    public DqRefreshCoordinator(IServiceScopeFactory scopeFactory, ILogger<DqRefreshCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Starts the refresh in the background; false (409) when one already runs.</summary>
    public bool TryStart()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return false;

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Data quality refresh started");
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DqOverviewService>().RefreshAsync(CancellationToken.None);
                _logger.LogInformation("Data quality refresh finished");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data quality refresh failed");
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        });
        return true;
    }
}

/// <summary>Builds the overview tiles from the snapshot and performs the refresh (view refresh + stored check counts for the delta).</summary>
public sealed class DqOverviewService
{
    private readonly IDqFactsRepository _facts;
    private readonly IDqCheckRepository _checks;
    private readonly DqRefreshCoordinator _coordinator;
    private readonly IMemoryCache _cache;

    public DqOverviewService(IDqFactsRepository facts, IDqCheckRepository checks, DqRefreshCoordinator coordinator, IMemoryCache cache)
    {
        _facts = facts;
        _checks = checks;
        _coordinator = coordinator;
        _cache = cache;
    }

    private sealed record Snapshot(OverviewTotals Totals, List<OverviewCheck> Checks);

    public async Task<OverviewResponse> GetAsync(DqFacets facets, CancellationToken ct)
    {
        var state = await _facts.GetStateAsync(ct);
        var response = new OverviewResponse
        {
            RefreshedAt = state.RefreshedAt,
            StatsRefreshedAt = state.StatsRefreshedAt,
            LinkRolesAppliedAt = state.LinkRolesAppliedAt,
            Refreshing = _coordinator.IsRunning,
            LastEditAt = await _facts.GetLastEditAtAsync(ct),
            SeedImports = [.. await _facts.GetSeedImportsAsync(ct)],
        };
        if (state.RefreshedAt is null || !state.IsPopulated) return response;

        var key = $"dq:overview:{state.RefreshedAt.Value.Ticks}:{facets.Testament}:{facets.BookId}";
        var snapshot = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return await ComputeAsync(facets, ct);
        });

        var previous = facets.Testament is null && facets.BookId is null && state.PreviousCounts is not null
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(state.PreviousCounts)
            : null;
        response.Totals = snapshot!.Totals;
        response.Checks = [.. snapshot.Checks.Select(c => new OverviewCheck
        {
            Code = c.Code,
            Tier = c.Tier,
            Count = c.Count,
            ErrorCount = c.ErrorCount,
            Sub = c.Sub,
            PreviousCount = previous is not null && previous.TryGetValue(c.Code, out var prev) ? prev : null,
        })];
        return response;
    }

    private async Task<Snapshot> ComputeAsync(DqFacets facets, CancellationToken ct)
    {
        var totalsTask = _facts.GetTotalsAsync(facets, ct);
        var counts = await Task.WhenAll(DqChecks.Codes.Select(code => _checks.CountAsync(code, facets, ct)));
        return new Snapshot(
            await totalsTask,
            [.. DqChecks.Codes.Select((code, i) => new OverviewCheck { Code = code, Tier = 1, Count = counts[i].Count, ErrorCount = counts[i].ErrorCount, Sub = counts[i].Sub })]);
    }

    /// <summary>
    /// Facts first (RefreshedAt and the check counts are saved), then the Stats* views and StatsRefreshedAt. If the stats step fails the exception
    /// propagates to the coordinator (logged, flag released) and StatsRefreshedAt keeps its old value while RefreshedAt is already new.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct)
    {
        await _facts.RefreshViewAsync(ct);
        var snapshot = await ComputeAsync(new DqFacets(null, null), ct);
        await _facts.SaveCountsAsync(snapshot.Checks.ToDictionary(c => c.Code, c => c.Count), ct);

        await _facts.RefreshStatsViewsAsync(ct);
        await _facts.SaveStatsRefreshedAtAsync(ct);
    }
}
