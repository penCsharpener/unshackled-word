using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

/// <summary>
/// Startup check: the in-code STTS list must agree with the SttsTags table. Differences are logged as an error and never stop the API;
/// an empty table (not seeded yet) is only a warning.
/// </summary>
public sealed class SttsDriftCheck : IHostedService
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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var tags = await scope.ServiceProvider.GetRequiredService<ISttsTagRepository>().GetAllAsync(ct);
            if (tags.Count == 0)
            {
                _logger.LogWarning("SttsTags is empty (not seeded yet); the STTS list in code cannot be checked");
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
