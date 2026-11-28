using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Seeding.Abstractions;
using UnshackledWord.Seeding.StepBible;

namespace UnshackledWord.Seeding.Swete;

/// <summary>
/// Applies <c>assets/seed/lxx/tags.csv</c> and <c>links.csv</c> right after the LXX text import (roadmap 0028.8), only while <c>LxxWordTags</c> has no confirmed row
/// (a fresh database); the logic lives in <see cref="LxxSeedService.ApplyInitialSeedAsync(CancellationToken)"/>. Without LXX text every line is skipped as an anchor mismatch.
/// When this run's STEP import inserted Hebrew words (a STEP re-import renumbers <c>StepHebrewWords</c>), existing <c>LxxHebrewLinks</c> are stale: they are deleted and
/// the links of the seed files re-resolved first (<see cref="LxxSeedService.RelinkAfterStepImportAsync(CancellationToken)"/>, risk "Hebrew link anchors").
/// </summary>
public sealed class LxxTagSeedRunner : IRunner
{
    private readonly LxxSeedService _service;
    private readonly StepImportResult _stepImport;
    private readonly ILogger<LxxTagSeedRunner> _logger;

    public LxxTagSeedRunner(LxxSeedService service, StepImportResult stepImport, ILogger<LxxTagSeedRunner> logger)
    {
        _service = service;
        _stepImport = stepImport;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        if (_stepImport.InsertedHebrewWords > 0)
        {
            var relink = await _service.RelinkAfterStepImportAsync(token);
            if (relink is not null)
            {
                _logger.LogInformation("LXX Hebrew links re-resolved after the STEP import: {Counts}", string.Join(", ", relink.Counts.Select(kv => $"{kv.Key}={kv.Value}")));
            }
        }

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
