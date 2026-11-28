using UnshackledWord.Infrastructure.Seed.Tsk;
using UnshackledWord.Seeding.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

/// <summary>
/// Applies <c>assets/seed/tsk/curations.csv</c> right after the TSK import (roadmap 0024.7), only while <c>TskCurations</c> is empty (a fresh
/// database); the logic lives in <see cref="TskCurationSeedService.ApplyInitialSeedAsync(CancellationToken)"/>.
/// </summary>
public sealed class TskCurationSeedRunner : IRunner
{
    private readonly TskCurationSeedService _service;
    private readonly ILogger<TskCurationSeedRunner> _logger;

    public TskCurationSeedRunner(TskCurationSeedService service, ILogger<TskCurationSeedRunner> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var report = await _service.ApplyInitialSeedAsync(token);

        if (report.Skipped)
        {
            _logger.LogInformation("TSK curation seed skipped: {Reason}", report.Reason);
            return;
        }

        _logger.LogInformation("TSK curation seed applied: {Counts}", string.Join(", ", report.Counts.Select(kv => $"{kv.Key}={kv.Value}")));
    }
}
