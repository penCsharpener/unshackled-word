using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>lxx-prefill</c> (roadmap 0028.6): pass 1 of the LXX tagging through <see cref="LxxNtFormTagger"/>. Needs the database with the Swete text
/// and the STEP Greek words. Writes <c>nt-form</c> proposals (no user), never touches confirmed, ai or human rows, and is idempotent. Logs coverage per
/// book and overall. Exit codes: 0 done, 1 failed.
/// </summary>
public sealed class LxxPrefillVerb : ISeedVerb
{
    private readonly LxxNtFormTagger _tagger;
    private readonly ILogger<LxxPrefillVerb> _logger;

    public LxxPrefillVerb(LxxNtFormTagger tagger, ILogger<LxxPrefillVerb> logger)
    {
        _tagger = tagger;
        _logger = logger;
    }

    public string Name => "lxx-prefill";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        try
        {
            await _tagger.RunAsync(progress: null, token);
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "lxx-prefill failed; nothing was written");
            return 1;
        }
    }
}
