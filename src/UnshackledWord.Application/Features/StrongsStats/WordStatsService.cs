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

    public async Task<GetWordStatsResponse?> GetAsync(string word, string testament, CancellationToken token = default)
    {
        var range = ResolveTestament(testament);
        if (range is null)
        {
            return null;
        }

        var (name, first, last) = range.Value;

        var key = word.Trim().ToLowerInvariant();
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
