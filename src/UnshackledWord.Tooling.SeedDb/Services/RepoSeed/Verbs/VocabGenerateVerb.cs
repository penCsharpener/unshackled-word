using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>vocab-generate</c>: builds or refreshes the vocab lexicon (roadmap 0022.1) from <c>StepStrongsLexicon</c> and the STEP word tables.
/// The work is done by <see cref="VocabLexiconGenerator"/> (also behind <c>POST api/maintainer/vocab/generate</c>); idempotent.
/// </summary>
public sealed class VocabGenerateVerb : ISeedVerb
{
    private readonly VocabLexiconGenerator _generator;
    private readonly ILogger<VocabGenerateVerb> _logger;

    public VocabGenerateVerb(VocabLexiconGenerator generator, ILogger<VocabGenerateVerb> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    public string Name => "vocab-generate";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var report = await _generator.GenerateAsync(new Progress<string>(line => _logger.LogInformation("{Line}", line)), token);

        foreach (var count in report.Counts.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            _logger.LogInformation("{Name}: {Value}", count.Key, count.Value);
        }

        return 0;
    }
}
