using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

/// <summary>
/// Startup check: the in-code STTS list must agree with the SttsTags table. Differences are logged as an error and never stop the API;
/// an empty table (not seeded yet) is a single information line.
/// </summary>
public sealed class SttsDriftCheck : IHostedService, ISeedDataChanged
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SttsDriftCheck> _logger;

    public SttsDriftCheck(IServiceScopeFactory scopeFactory, ILogger<SttsDriftCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(CancellationToken.None), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>After the STTS tags were seeded the check runs again (it logged "no tags yet" at startup).</summary>
    public Task SeedDataChangedAsync(string stepKey, CancellationToken token) => stepKey == "stts" ? RunAsync(token) : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var tags = await scope.ServiceProvider.GetRequiredService<ISttsTagRepository>().GetAllAsync(ct);
            if (tags.Count == 0)
            {
                _logger.LogInformation("SttsTags: no tags yet (not seeded); the STTS list in code is checked after the first seed and the next start");
                return;
            }

            var differences = SttsTagDrift.Compare(ElbLemmaValueMaps.Get(ElbLemmaValueMaps.Stts).Values, tags);
            if (differences.Count == 0)
            {
                _logger.LogInformation("STTS list in code agrees with SttsTags ({Count} tags)", tags.Count);
                return;
            }

            _logger.LogError("STTS list in code and SttsTags table differ: {Differences}", string.Join("; ", differences));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STTS drift check could not run");
        }
    }
}
