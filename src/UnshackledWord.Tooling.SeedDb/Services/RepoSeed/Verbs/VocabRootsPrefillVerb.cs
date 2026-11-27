using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>vocab-roots-prefill</c>: the deterministic root prefill of the vocab entries (roadmap 0030.2), also run by <c>vocab-generate</c>.
/// Writes only keys without a root or with origin <c>prefill</c>; idempotent.
/// </summary>
public sealed class VocabRootsPrefillVerb : ISeedVerb
{
    private readonly VocabRootPrefillService _service;
    private readonly ILogger<VocabRootsPrefillVerb> _logger;

    public VocabRootsPrefillVerb(VocabRootPrefillService service, ILogger<VocabRootsPrefillVerb> logger)
    {
        _service = service;
        _logger = logger;
    }

    public string Name => "vocab-roots-prefill";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var report = await _service.PrefillAsync(new Progress<string>(line => _logger.LogInformation("{Line}", line)), token);

        foreach (var count in report.Counts.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            _logger.LogInformation("{Name}: {Value}", count.Key, count.Value);
        }

        foreach (var item in report.Lists.GetValueOrDefault("greekUnresolved")?.Items ?? [])
        {
            _logger.LogInformation("unresolved {Item}", item);
        }

        return 0;
    }
}
