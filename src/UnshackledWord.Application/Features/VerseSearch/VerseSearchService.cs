using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Application.Features.VerseSearch;

/// <summary>
/// Verse search: parse, stem (ts_lexize), widen single words by their lemmas (forms -> lemma resolution of the stats snapshot; stem-only
/// while the snapshot was never computed), build the tsquery, count per book, fetch one ranked page and mark the matching words.
/// </summary>
public sealed class VerseSearchService : IVerseSearchService
{
    /// <summary>A word like "sein" or "Liebe" resolves to several lemmas; the most frequent ones are enough and keep the query small.</summary>
    public const int MaxLemmasPerWord = 3;

    private readonly IVerseSearchRepository _verses;
    private readonly IStrongsStatsRepository _stats;

    public VerseSearchService(IVerseSearchRepository verses, IStrongsStatsRepository stats)
    {
        _verses = verses;
        _stats = stats;
    }

    public async Task<SearchVersesResponse?> SearchAsync(string query, int page, int pageSize, int? bookId, CancellationToken token = default)
    {
        var parsed = VerseSearchQueryParser.Parse(query);
        if (!parsed.HasPositive)
        {
            return null;
        }

        var atoms = parsed.AllWords.SelectMany(VerseSearchQueryBuilder.Atoms).Distinct(StringComparer.Ordinal).ToList();
        var stems = (await _verses.GetLexemesAsync(atoms, token))
            .Where(r => !string.IsNullOrEmpty(r.Lexeme))
            .ToDictionary(r => r.Word, r => r.Lexeme!, StringComparer.Ordinal);

        var lemmas = await ResolveLemmasAsync(parsed, token);
        var built = VerseSearchQueryBuilder.Build(parsed, stems, lemmas);

        // Both queries scan the matching verses; they run side by side (own connections) so the slowest one decides the latency.
        var offset = (page - 1) * pageSize;
        var (first, last) = bookId is { } b ? (b * 1_000_000, b * 1_000_000 + 999_999) : (0, int.MaxValue);
        var literal = built.LiteralTsQuery == built.TsQuery ? null : built.LiteralTsQuery;
        var bookCountsTask = _verses.GetBookCountsAsync(built.TsQuery, token);
        var rowsTask = _verses.GetHitsAsync(built.TsQuery, literal, first, last, offset, pageSize, token);
        await Task.WhenAll(bookCountsTask, rowsTask);

        var bookCounts = bookCountsTask.Result;
        var total = bookId is null ? bookCounts.Sum(c => c.Count) : bookCounts.Where(c => c.BookId == bookId).Sum(c => c.Count);

        var hits = new List<SearchVersesHit>();
        var rows = rowsTask.Result;
        if (rows.Count > 0)
        {
            var words = (await _verses.GetWordsAsync(rows.Select(r => r.HebRefId).ToList(), token)).ToLookup(w => w.HebRefId);
            hits = rows.Select(r => ToHit(r.HebRefId, words[r.HebRefId], built)).ToList();
        }

        return new SearchVersesResponse
        {
            Query = query.Trim(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            Tokens = built.Tokens.Select(t => new SearchVersesToken
            {
                Text = t.Text,
                Lexemes = t.Lexemes.ToList(),
                Lemmas = t.Lemmas.Count == 0 ? null : t.Lemmas.ToList()
            }).ToList(),
            BookCounts = bookCounts.Select(c => new SearchVersesBookCount { BookId = c.BookId, Count = c.Count }).ToList(),
            Hits = hits
        };
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ResolveLemmasAsync(VerseSearchQuery query, CancellationToken token)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (await _stats.GetSnapshotRefreshedAtAsync(token) is null)
        {
            return result;
        }

        var words = query.Groups.SelectMany(g => g)
            .Where(t => !t.Negated && !t.IsPhrase)
            .Select(t => t.Words[0])
            .Distinct(StringComparer.Ordinal);

        foreach (var word in words)
        {
            var rows = await _stats.ResolveSnapshotLemmasAsync(word, token);
            var keys = rows
                .Select(r => r.Key)
                .Where(VerseSearchQueryBuilder.IsPlainLemma)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxLemmasPerWord)
                .ToList();
            if (keys.Count > 0)
            {
                result[word] = keys;
            }
        }

        return result;
    }

    private static SearchVersesHit ToHit(int hebRefId, IEnumerable<VerseSearchWordRow> words, BuiltVerseQuery query)
    {
        return new SearchVersesHit
        {
            BibleBookId = hebRefId / 1_000_000,
            Chapter = hebRefId / 1_000 % 1_000,
            Verse = hebRefId % 1_000,
            Tokens = words.Select(w => new SearchVersesHitToken { Text = w.WordInContext, Matched = IsMatch(w, query) }).ToList()
        };
    }

    private static bool IsMatch(VerseSearchWordRow word, BuiltVerseQuery query)
    {
        return word.Stems.Any(query.MatchStems.Contains) || (word.Lemma is not null && query.MatchLemmas.Contains(word.Lemma));
    }
}
