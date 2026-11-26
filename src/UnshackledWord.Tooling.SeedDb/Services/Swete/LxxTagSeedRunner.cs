using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Swete;

/// <summary>
/// Applies <c>assets/seed/lxx/tags.csv</c> and <c>links.csv</c> right after the LXX text import (roadmap 0028.8), only while <c>LxxWordTags</c> has no confirmed row
/// (a fresh database); the logic lives in <see cref="LxxSeedService.ApplyInitialSeedAsync(CancellationToken)"/>. Without LXX text every line is skipped as an anchor mismatch.
/// </summary>
public sealed class LxxTagSeedRunner : IRunner
{
    private readonly LxxSeedService _service;
    private readonly ILogger<LxxTagSeedRunner> _logger;

    public LxxTagSeedRunner(LxxSeedService service, ILogger<LxxTagSeedRunner> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var report = await _service.ApplyInitialSeedAsync(token);

        if (report.Skipped)
        {
            if (report.Reason == SeedSkipReason.TablesNotEmpty)
            {
                _logger.LogInformation("LXX tags up to date, no import");
            }
            else
            {
                _logger.LogInformation("LXX tag seed skipped: {Reason}", report.Reason);
            }

            return;
        }

        _logger.LogInformation("LXX tag seed applied: {Counts}", string.Join(", ", report.Counts.Select(kv => $"{kv.Key}={kv.Value}")));
    }
}
