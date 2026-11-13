using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

namespace UnshackledWord.Application.Features.StrongsStats;

public sealed class WordStatsService : IWordStatsService
{
    private const int LastOldTestamentBook = 39;
    private const int LastBook = 66;

    private readonly IStrongsStatsRepository _stats;

    public WordStatsService(IStrongsStatsRepository stats)
    {
        _stats = stats;
    }

    /// <summary>Book range of a testament ("all", "ot", "nt", case-insensitive), or null for anything else.</summary>
    public static (string Testament, int FirstBook, int LastBook)? ResolveTestament(string? testament)
    {
        return testament?.Trim().ToLowerInvariant() switch
        {
            "all" => ("all", 1, LastBook),
            "ot" => ("ot", 1, LastOldTestamentBook),
            "nt" => ("nt", LastOldTestamentBook + 1, LastBook),
            _ => null
        };
    }

    public async Task<GetWordStatsResponse?> GetAsync(string word, string testament, string? pos = null, CancellationToken token = default)
    {
        var range = ResolveTestament(testament);
        if (range is null)
        {
            return null;
        }

        var (name, first, last) = range.Value;

        var key = word.Trim().ToLowerInvariant();
        var statsRefreshedAt = await _stats.GetSnapshotRefreshedAtAsync(token);
        if (statsRefreshedAt is not null)
        {
            return await GetFromSnapshotAsync(word, key, name, first, last, pos, statsRefreshedAt.Value, token);
        }

        var bookRows = await _stats.GetWordBookCountsAsync(key, first, last, token);
        var originals = bookRows.Count == 0 ? [] : await _stats.GetWordOriginalsAsync(key, first, last, token);

        return new GetWordStatsResponse
        {
            Word = bookRows.Count == 0 ? word : bookRows.MaxBy(r => r.Count)!.Word,
            Testament = name,
            OccurrenceCount = bookRows.Sum(r => r.Count),
            VerseCount = bookRows.Sum(r => r.VerseCount),
            UnmappedCount = bookRows.Sum(r => r.UnmappedCount),
            ByBook = bookRows.Select(r => new StrongsBookCountResponse { BibleBookId = r.BibleBookId, Count = r.Count, VerseCount = r.VerseCount }).ToList(),
            Originals = originals.Select(ToResponse).ToList()
        };
    }

    /// <summary>
    /// Snapshot path: the word is resolved to lemma(s) + PoS through the form table (a lemma key only if it is no known form); the stats are those of
    /// the whole lemma (all its forms). The PoS rows of the most frequent lemma are summed; the PoS filter narrows the candidates first.
    /// </summary>
    private async Task<GetWordStatsResponse> GetFromSnapshotAsync(string word, string key, string testament, int first, int last, string? pos, DateTime statsRefreshedAt, CancellationToken token)
    {
        var resolved = await _stats.ResolveSnapshotLemmasAsync(key, token);
        var response = new GetWordStatsResponse { Word = word, Testament = testament, StatsRefreshedAt = statsRefreshedAt };
        if (resolved.Count == 0)
        {
            return response;
        }

        response.Candidates = resolved.Select(r => new WordLemmaCandidate { Lemma = r.Word, Pos = r.Pos, Occurrences = r.Occurrences }).ToList();

        var filtered = FilterByPos(resolved, pos);
        if (filtered.Count == 0)
        {
            return response;
        }

        // Several lemmas for one form (e.g. "ging" is a form of "gehen" and, through a tagging error, a lemma of its own): the most frequent lemma
        // answers; its other PoS rows are summed with it. The rest stays visible in Candidates.
        var topKey = filtered.GroupBy(r => r.Key).MaxBy(g => g.Sum(r => r.Occurrences))!.Key;
        var selected = filtered.Where(r => r.Key == topKey).ToList();

        var top = selected.MaxBy(r => r.Occurrences)!;
        response.Word = top.Word;
        response.Lemma = top.Word;
        response.Pos = selected.Count == 1 ? selected[0].Pos : string.IsNullOrWhiteSpace(pos) ? null : top.Pos;

        var books = SnapshotBookCounts.Merge(selected.Select(r => r.BookCounts), first, last);
        response.OccurrenceCount = books.Sum(b => b.C);
        response.VerseCount = books.Sum(b => b.V);
        response.UnmappedCount = books.Sum(b => b.U);
        response.ByBook = books.Select(b => new StrongsBookCountResponse { BibleBookId = b.B, Count = b.C, VerseCount = b.V }).ToList();

        if (response.OccurrenceCount > 0)
        {
            // Greek words exist only in the New Testament books (40+), Hebrew/Aramaic only before: the testament decides the language.
            var lang = testament switch { "ot" => "H", "nt" => "G", _ => null };
            var originals = await _stats.GetSnapshotWordOriginalsAsync(selected.Select(r => new SnapshotLemmaKey(r.Key, r.Pos)).ToList(), lang, token);
            response.Originals = originals.Select(ToResponse).ToList();
        }

        return response;
    }

    /// <summary>The PoS filter is case-insensitive; null or blank keeps every row.</summary>
    public static List<SnapshotLemmaRow> FilterByPos(IReadOnlyList<SnapshotLemmaRow> rows, string? pos)
    {
        return string.IsNullOrWhiteSpace(pos)
            ? rows.ToList()
            : rows.Where(r => string.Equals(r.Pos, pos.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static WordOriginalResponse ToResponse(WordOriginalRow row)
    {
        return new WordOriginalResponse
        {
            Strongs = row.Strongs,
            Language = row.Strongs.StartsWith('G') ? "greek" : row.LexiconLanguageId == 1 ? "aramaic" : "hebrew",
            Lemma = row.Lemma,
            Transliteration = row.Transliteration,
            Gloss = row.Gloss,
            Count = row.Count,
            TotalCount = row.TotalCount
        };
    }
}
