using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>vocab-derive [--course nt-greek|ot-hebrew] [--from N] [--to N] [--dry-run]</c>: derives the vocab meanings from the live renderings
/// (roadmap 0022.3). The work is done by <see cref="VocabMeaningDeriver"/> (also behind <c>POST api/maintainer/vocab/meanings/derive</c>); a rerun changes nothing.
/// </summary>
public sealed class VocabDeriveVerb : ISeedVerb
{
    private readonly VocabMeaningDeriver _deriver;
    private readonly ILogger<VocabDeriveVerb> _logger;

    public VocabDeriveVerb(VocabMeaningDeriver deriver, ILogger<VocabDeriveVerb> logger)
    {
        _deriver = deriver;
        _logger = logger;
    }

    public string Name => "vocab-derive";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var scope = new VocabMeaningScope(arguments.Get("--course"), ParseInt(arguments.Get("--from")), ParseInt(arguments.Get("--to")));
        var report = await _deriver.DeriveAsync(scope, arguments.Has("--dry-run"), null, new Progress<string>(line => _logger.LogInformation("{Line}", line)), token);

        foreach (var count in report.Counts.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            _logger.LogInformation("{Name}: {Value}", count.Key, count.Value);
        }

        foreach (var item in report.Lists["topMeaningChanged"].Items.Take(30))
        {
            _logger.LogInformation("top meaning: {Item}", item);
        }

        return 0;
    }

    private static int? ParseInt(string? value) => int.TryParse(value, out var n) ? n : null;
}
