using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchOriginals;

namespace UnshackledWord.Application.Features.StrongsStats;

/// <summary>Smart search lookup: per token the German lemma + PoS it resolves to and the originals those lemmas render, from the stats snapshot.</summary>
public sealed class SearchOriginalsService : ISearchOriginalsService
{
    private readonly IStrongsStatsRepository _stats;

    public SearchOriginalsService(IStrongsStatsRepository stats)
    {
        _stats = stats;
    }

    public async Task<SearchOriginalsResponse?> LookupAsync(string query, int top, bool includeFrequent, CancellationToken token = default)
    {
        var statsRefreshedAt = await _stats.GetSnapshotRefreshedAtAsync(token);
        if (statsRefreshedAt is null)
        {
            return null;
        }

        var resolved = new List<(string Token, IReadOnlyList<SnapshotLemmaRow> Lemmas)>();
        foreach (var tokenText in SearchTokenizer.Tokenize(query))
        {
            resolved.Add((tokenText, await _stats.ResolveSnapshotLemmasAsync(tokenText, token)));
        }

        var wanted = resolved
            .SelectMany(r => r.Lemmas)
            .Where(l => includeFrequent || !StatsRules.IsHighFrequency(l.LemmaOccurrences))
            .Select(l => new SnapshotLemmaKey(l.Key, l.Pos))
            .Distinct()
            .ToList();

        var originals = wanted.Count == 0 ? [] : await _stats.GetSnapshotLemmaOriginalsAsync(wanted, top, token);
        var byLemma = originals.ToLookup(o => new SnapshotLemmaKey(o.Key, o.Pos));

        return new SearchOriginalsResponse
        {
            StatsRefreshedAt = statsRefreshedAt.Value,
            Tokens = resolved.Select(r => new SearchOriginalsToken
            {
                Token = r.Token,
                Lemmas = r.Lemmas.Select(l => ToLemma(l, includeFrequent, byLemma)).ToList()
            }).ToList()
        };
    }

    private static SearchOriginalsLemma ToLemma(SnapshotLemmaRow row, bool includeFrequent, ILookup<SnapshotLemmaKey, SnapshotLemmaOriginalRow> originals)
    {
        var frequent = StatsRules.IsHighFrequency(row.LemmaOccurrences);
        return new SearchOriginalsLemma
        {
            Lemma = row.Key,
            Word = row.Word,
            Pos = row.Pos,
            Occurrences = row.Occurrences,
            LemmaOccurrences = row.LemmaOccurrences,
            HighFrequency = frequent,
            Originals = frequent && !includeFrequent
                ? []
                : originals[new SnapshotLemmaKey(row.Key, row.Pos)].Select(ToOriginal).ToList()
        };
    }

    private static SearchOriginalResponse ToOriginal(SnapshotLemmaOriginalRow row)
    {
        return new SearchOriginalResponse
        {
            Strongs = row.Strongs,
            Language = row.Strongs.StartsWith('G') ? "greek" : row.LexiconLanguageId == 1 ? "aramaic" : "hebrew",
            Lemma = row.Lemma,
            Gloss = row.Gloss,
            Count = row.Count,
            Share = row.LemmaTotal == 0 ? 0 : Math.Round((double)row.Count / row.LemmaTotal, 4),
            TotalCount = row.TotalCount
        };
    }
}
