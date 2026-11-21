using UnshackledWord.Tooling.SeedDb.Services.StepBible;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>step-lexicon-repair</c>: adds the disambiguated Hebrew rows missing in <c>StepStrongsLexicon</c> (roadmap 0022.1b); idempotent.
/// Run <c>vocab-generate</c> afterwards.
/// </summary>
public sealed class StepLexiconRepairVerb : ISeedVerb
{
    private readonly StepLexiconRepairService _service;
    private readonly ILogger<StepLexiconRepairVerb> _logger;

    public StepLexiconRepairVerb(StepLexiconRepairService service, ILogger<StepLexiconRepairVerb> logger)
    {
        _service = service;
        _logger = logger;
    }

    public string Name => "step-lexicon-repair";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        var added = await _service.RepairAsync(token);
        _logger.LogInformation("StepStrongsLexicon: {Count} missing rows added", added.Count);

        return 0;
    }
}
