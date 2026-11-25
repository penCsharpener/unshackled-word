using System.Collections.Concurrent;

namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>
/// In-memory corpus statistics for the read-time explanations (roadmap 0027.3): per testament the corpus built with the parameters of the run
/// whose pairs are stored (df, stop list, idf, token sequences), plus the lexicon labels already looked up. Registered as a singleton. The
/// entry belongs to one <c>RunId</c>; the first request that sees a hit of another run (a rebuild finished) replaces it, so nothing is stale
/// after a rebuild and nothing is loaded per request. Each testament is built once on first use (a second or two), later requests only read.
/// </summary>
public sealed class VerseSimilarityExplainerCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Entry? _entry;

    /// <summary>The run whose corpora are held, or null (diagnostics and tests).</summary>
    public int? CachedRunId => _entry?.RunId;

    public async Task<VerseSimilarityCorpus> GetCorpusAsync(
        int runId,
        Testament testament,
        Func<CancellationToken, Task<VerseSimilarityParameters>> loadParameters,
        Func<Testament, CancellationToken, Task<IReadOnlyList<VerseTokenSequence>>> loadTokens,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_entry is null || _entry.RunId != runId)
            {
                _entry = new Entry(runId, await loadParameters(token));
            }

            if (!_entry.Corpora.TryGetValue(testament, out var corpus))
            {
                var tokens = await loadTokens(testament, token);
                corpus = VerseSimilarityScorer.BuildCorpus(tokens, _entry.Parameters);
                _entry.Corpora[testament] = corpus;
            }

            return corpus;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Labels of the Strong's numbers; looks up only the ones not seen for this run yet. Unknown numbers are missing from the result.</summary>
    public async Task<IReadOnlyDictionary<string, StrongsLabel>> GetLabelsAsync(
        int runId,
        IReadOnlyCollection<string> strongs,
        Func<IReadOnlyCollection<string>, Task<IReadOnlyDictionary<string, StrongsLabel>>> load)
    {
        var entry = _entry;
        if (entry is null || entry.RunId != runId)
        {
            return await load(strongs);
        }

        var missing = strongs.Where(s => !entry.Labels.ContainsKey(s)).Distinct(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            var loaded = await load(missing);
            foreach (var s in missing)
            {
                entry.Labels[s] = loaded.GetValueOrDefault(s);
            }
        }

        var result = new Dictionary<string, StrongsLabel>(StringComparer.Ordinal);
        foreach (var s in strongs)
        {
            if (entry.Labels.TryGetValue(s, out var label) && label is not null)
            {
                result[s] = label;
            }
        }

        return result;
    }

    private sealed class Entry(int runId, VerseSimilarityParameters parameters)
    {
        public int RunId { get; } = runId;
        public VerseSimilarityParameters Parameters { get; } = parameters;
        public Dictionary<Testament, VerseSimilarityCorpus> Corpora { get; } = [];
        public ConcurrentDictionary<string, StrongsLabel?> Labels { get; } = new(StringComparer.Ordinal);
    }
}
