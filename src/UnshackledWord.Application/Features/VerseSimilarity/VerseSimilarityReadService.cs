using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;

namespace UnshackledWord.Application.Features.VerseSimilarity;

public sealed class VerseSimilarityReadService : IVerseSimilarityReadService
{
    public const int MaxLimit = 30;
    public const int DefaultLimit = 10;

    private readonly IVerseSimilarityRepository _repository;
    private readonly IVerseTokenRepository _tokens;
    private readonly VerseSimilarityExplainerCache _cache;
    private readonly ILogger<VerseSimilarityReadService> _logger;

    public VerseSimilarityReadService(IVerseSimilarityRepository repository, IVerseTokenRepository tokens, VerseSimilarityExplainerCache cache,
        ILogger<VerseSimilarityReadService> logger)
    {
        _repository = repository;
        _tokens = tokens;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GetSimilarVersesResponse> GetChapterAsync(int bibleBookId, int chapter, int limit, CancellationToken token = default)
    {
        var (from, to) = RevisionSql.ChapterRange(bibleBookId, chapter);
        var verses = await ReadAsync(from, to, limit, token);
        return new GetSimilarVersesResponse { BibleBookId = bibleBookId, Chapter = chapter, Verses = verses };
    }

    public async Task<VerseSimilarVersesResponse?> GetVerseAsync(int hebRefId, CancellationToken token = default)
    {
        var verses = await ReadAsync(hebRefId, hebRefId, MaxLimit, token);
        return verses.FirstOrDefault();
    }

    private async Task<List<VerseSimilarVersesResponse>> ReadAsync(int from, int to, int limit, CancellationToken token)
    {
        var page = await _repository.GetPageAsync(from, to, Math.Clamp(limit, 1, MaxLimit), token);

        // Resolve and thin out: unresolved targets (no Elberfelder verse for the STEP key), the verse itself and double targets are dropped.
        var kept = new List<(SimilarHitRow Row, int TargetHebRefId)>();
        var unresolved = 0;
        foreach (var group in page.Hits.GroupBy(h => h.SourceHebRefId))
        {
            var seen = new HashSet<int>();
            foreach (var row in group.OrderBy(h => h.Rank))
            {
                if (row.TargetHebRefId is not { } target)
                {
                    unresolved++;
                    continue;
                }

                if (target != row.SourceHebRefId && seen.Add(target))
                {
                    kept.Add((row, target));
                }
            }
        }

        if (unresolved > 0)
        {
            _logger.LogDebug("Similar verses {From}-{To}: dropped {Unresolved} hit(s) whose target has no Elberfelder verse", from, to, unresolved);
        }

        var explanations = new Dictionary<(int Source, int Target), VerseSimilarityExplanation?>();
        IReadOnlyDictionary<string, StrongsLabel> labels = new Dictionary<string, StrongsLabel>();
        if (kept.Count > 0)
        {
            var runId = kept[0].Row.RunId;
            var corpora = new Dictionary<Testament, VerseSimilarityCorpus>();
            foreach (var testament in kept.Select(k => SimilarVerseMapper.TestamentOf(k.Row.SourceLxxRefId)).Distinct())
            {
                corpora[testament] = await _cache.GetCorpusAsync(runId, testament, LoadParametersAsync(runId), _tokens.LoadAsync, token);
            }

            foreach (var (row, _) in kept)
            {
                var key = (row.SourceLxxRefId, row.TargetLxxRefId);
                if (!explanations.ContainsKey(key))
                {
                    explanations[key] = VerseSimilarityScorer.Explain(corpora[SimilarVerseMapper.TestamentOf(row.SourceLxxRefId)], key.SourceLxxRefId, key.TargetLxxRefId);
                }
            }

            var wanted = explanations.Values.SelectMany(SimilarVerseMapper.StrongsOf).Distinct(StringComparer.Ordinal).ToList();
            labels = await _cache.GetLabelsAsync(runId, wanted, strongs => _tokens.GetLabelsAsync(strongs, token));
        }

        var hitsBySource = kept.ToLookup(k => k.Row.SourceHebRefId);
        return page.Sources
            .Select(source =>
            {
                var reference = BibleReference.FromRefId(source.HebRefId);
                return new VerseSimilarVersesResponse
                {
                    HebRefId = source.HebRefId,
                    BibleBookId = reference.BookId,
                    Chapter = reference.Chapter,
                    Verse = reference.Verse,
                    Hits = hitsBySource[source.HebRefId]
                        .Take(MaxLimit)
                        .Select(k => SimilarVerseMapper.ToHit(k.Row, k.TargetHebRefId, explanations.GetValueOrDefault((k.Row.SourceLxxRefId, k.Row.TargetLxxRefId)), labels))
                        .ToList(),
                };
            })
            .ToList();
    }

    private Func<CancellationToken, Task<VerseSimilarityParameters>> LoadParametersAsync(int runId) =>
        async ct =>
        {
            var run = await _repository.GetRunAsync(runId, ct);
            return run is null
                ? VerseSimilarityParameters.Default
                : new VerseSimilarityParameters((double)run.StopDfShare, run.TopK, (double)run.MinScore, (double)run.PhraseWeight);
        };
}
