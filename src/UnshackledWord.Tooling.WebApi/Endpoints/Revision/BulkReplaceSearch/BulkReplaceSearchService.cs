using System.Text.RegularExpressions;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.BulkReplaceSearch;

public enum BulkReplaceSearchOutcome
{
    Ok,
    UnknownRevision,
}

public sealed class BulkReplaceSearchResult
{
    public BulkReplaceSearchOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public BulkReplaceSearchResponse? Response { get; init; }
}

/// <summary>Finds a German word in the source text of every verse (roadmap 0013.1/0013.2); the review and the saving use the existing revision endpoints.</summary>
public sealed partial class BulkReplaceSearchService
{
    /// <summary>STTS tags of articles and determiners that agree with the noun and need a manual look after a replacement.</summary>
    private static readonly HashSet<string> DeterminerTags = ["ART", "PPOSAT", "PDAT"];

    private readonly IBulkReplaceSearchRepository _repository;

    public BulkReplaceSearchService(IBulkReplaceSearchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BulkReplaceSearchResult> SearchAsync(BulkReplaceSearchRequest request)
    {
        var revisions = await _repository.GetRevisionsAsync();
        var word = request.Word.Trim();

        if (revisions.All(r => r.Code != request.SourceRevision))
        {
            return Failed("sourceRevision is not a revision code.");
        }

        var target = revisions.FirstOrDefault(r => r.Code == request.TargetRevision);

        if (target is null || target.IsBase)
        {
            return Failed("targetRevision must be a writable revision code.");
        }

        var lemmas = request.SameLemma ? (await _repository.GetLemmasAsync(word)).ToList() : [];
        var tokens = await _repository.GetHitVerseTokensAsync(request.SourceRevision, word, lemmas, BulkReplaceLimits.MaxRows + 1);
        var rows = BuildRows(tokens, word, lemmas);
        var capped = rows.Count > BulkReplaceLimits.MaxRows
            || tokens.Select(t => t.HebRefId).Distinct().Count() > BulkReplaceLimits.MaxRows;

        if (rows.Count > BulkReplaceLimits.MaxRows)
        {
            rows = rows.Take(BulkReplaceLimits.MaxRows).ToList();
        }

        await AddStrongsAsync(rows, tokens);
        await AddTargetAsync(rows, tokens, request.TargetRevision);

        return new BulkReplaceSearchResult
        {
            Outcome = BulkReplaceSearchOutcome.Ok,
            Response = new BulkReplaceSearchResponse
            {
                Lemmas = lemmas,
                Total = rows.Count,
                Capped = capped,
                Facets = BuildFacets(rows),
                Rows = rows,
            },
        };
    }

    /// <summary>One row per hit token; context, occurrence number and the article flag come from the neighbouring tokens of the verse.</summary>
    public static List<BulkReplaceRow> BuildRows(IReadOnlyList<BulkTokenRow> tokens, string word, IReadOnlyCollection<string> lemmas)
    {
        var rows = new List<BulkReplaceRow>();

        foreach (var verse in tokens.GroupBy(t => t.HebRefId))
        {
            var words = verse.OrderBy(t => t.PositionInVerse).ToList();
            var occurrence = 0;

            for (var i = 0; i < words.Count; i++)
            {
                var token = words[i];

                if (!IsHit(token, word, lemmas))
                {
                    continue;
                }

                var previous = i > 0 ? words[i - 1] : null;
                var from = Math.Max(0, i - BulkReplaceLimits.ContextWords);
                var to = Math.Min(words.Count, i + 1 + BulkReplaceLimits.ContextWords);
                rows.Add(new BulkReplaceRow
                {
                    HebRefId = token.HebRefId,
                    Occurrence = ++occurrence,
                    PositionInVerse = token.PositionInVerse,
                    Found = token.WordInContext,
                    ContextBefore = string.Join(' ', words.Skip(from).Take(i - from).Select(t => t.WordInContext)),
                    ContextAfter = string.Join(' ', words.Skip(i + 1).Take(to - i - 1).Select(t => t.WordInContext)),
                    Lemma = token.Lemma,
                    ArticleBefore = previous is not null && previous.Stts is { } stts && DeterminerTags.Contains(stts) ? previous.WordInContext : null,
                });
            }
        }

        return rows.OrderBy(r => r.HebRefId).ThenBy(r => r.Occurrence).ToList();
    }

    public static List<BulkReplaceFacet> BuildFacets(IReadOnlyList<BulkReplaceRow> rows)
    {
        var counts = new Dictionary<string, int>();
        var unlinked = 0;

        foreach (var row in rows)
        {
            if (row.Strongs.Count == 0)
            {
                unlinked++;
                continue;
            }

            foreach (var strongs in row.Strongs)
            {
                counts[strongs] = counts.GetValueOrDefault(strongs) + 1;
            }
        }

        var facets = counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => new BulkReplaceFacet { Strongs = c.Key, Count = c.Value }).ToList();

        if (unlinked > 0)
        {
            facets.Add(new BulkReplaceFacet { Strongs = null, Count = unlinked });
        }

        return facets;
    }

    /// <summary><c>H0802a</c> or <c>H0802</c> become <c>H802</c>; anything else is kept as is.</summary>
    public static string NormalizeStrongs(string strongs)
    {
        var match = StrongsPattern().Match(strongs);
        return match.Success ? match.Groups[1].Value + match.Groups[2].Value : strongs.Trim();
    }

    private static bool IsHit(BulkTokenRow token, string word, IReadOnlyCollection<string> lemmas) =>
        string.Equals(token.PlainWord, word, StringComparison.OrdinalIgnoreCase)
        || (token.Lemma is not null && lemmas.Contains(token.Lemma));

    private async Task AddStrongsAsync(List<BulkReplaceRow> rows, IReadOnlyList<BulkTokenRow> tokens)
    {
        var hits = rows.Select(r => tokens.First(t => t.HebRefId == r.HebRefId && t.PositionInVerse == r.PositionInVerse)).ToList();
        var baseIds = hits.Where(t => !t.IsVariant).Select(t => t.WordId).Distinct().ToList();
        var variantIds = hits.Where(t => t.IsVariant).Select(t => t.WordId).Distinct().ToList();
        var (baseRows, variantRows) = await _repository.GetStrongsAsync(baseIds, variantIds);
        var baseLookup = baseRows.Where(s => s.Strongs is not null).ToLookup(s => s.WordId, s => NormalizeStrongs(s.Strongs!));
        var variantLookup = variantRows.Where(s => s.Strongs is not null).ToLookup(s => s.WordId, s => NormalizeStrongs(s.Strongs!));

        foreach (var (row, token) in rows.Zip(hits))
        {
            var lookup = token.IsVariant ? variantLookup : baseLookup;
            row.Strongs = lookup[token.WordId].Distinct().ToList();
        }
    }

    private async Task AddTargetAsync(List<BulkReplaceRow> rows, IReadOnlyList<BulkTokenRow> tokens, string targetCode)
    {
        var verses = rows.Select(r => r.HebRefId).Distinct().ToList();
        var targets = (await _repository.GetTargetVersesAsync(targetCode, verses)).ToDictionary(t => t.HebRefId);
        var sourceText = tokens.GroupBy(t => t.HebRefId)
            .ToDictionary(g => g.Key, g => Normalize(string.Join(' ', g.OrderBy(t => t.PositionInVerse).Select(t => t.WordInContext))));

        foreach (var row in rows)
        {
            if (targets.TryGetValue(row.HebRefId, out var target))
            {
                row.TargetVersion = target.Version;
                row.TargetDiffers = Normalize(target.VerseText) != sourceText[row.HebRefId];
            }
        }
    }

    private static string Normalize(string text) => WhitespacePattern().Replace(text.Trim(), " ");

    private static BulkReplaceSearchResult Failed(string error) => new() { Outcome = BulkReplaceSearchOutcome.UnknownRevision, Error = error };

    [GeneratedRegex("([HG])0*(\\d+)")]
    private static partial Regex StrongsPattern();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespacePattern();
}
