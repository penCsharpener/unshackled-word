using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;

namespace UnshackledWord.Application.Features.StrongsStats;

public sealed class StrongsSummaryService : IStrongsSummaryService
{
    private readonly IStrongsLexiconRepository _lexicon;
    private readonly IStrongsStatsRepository _stats;

    public StrongsSummaryService(IStrongsLexiconRepository lexicon, IStrongsStatsRepository stats)
    {
        _lexicon = lexicon;
        _stats = stats;
    }

    public async Task<StrongsSummaryResult> GetAsync(string strongs, CancellationToken token = default)
    {
        if (!StrongsNumber.TryNormalize(strongs, out var normalized, out var hebrew))
        {
            return new StrongsSummaryResult(StrongsSummaryStatus.Invalid);
        }

        var parsed = StrongsNumber.Parse(normalized).Value!.Value;
        var entries = await _lexicon.GetEntriesAsync(parsed, token);
        var keys = StrongsMatchKeys.Resolve(normalized, parsed, entries);

        var bookRows = (await _stats.GetBookCountsAsync(keys, hebrew, token)).Where(r => r.BibleBookId > 0).ToList();
        var occurrenceCount = bookRows.Sum(r => r.Count);
        if (entries.Count == 0 && occurrenceCount == 0)
        {
            return new StrongsSummaryResult(StrongsSummaryStatus.NotFound);
        }

        var mapped = occurrenceCount == 0 ? 0 : await _stats.GetMappedCountAsync(keys, hebrew, token);
        IReadOnlyList<StrongsRenderingRow> renderings = occurrenceCount == 0 ? [] : await _stats.GetRenderingsAsync(keys, hebrew, token);

        var response = new GetStrongsSummaryResponse
        {
            Strongs = normalized,
            Language = ResolveLanguage(hebrew, entries, bookRows.Sum(r => r.AramaicCount), occurrenceCount),
            OccurrenceCount = occurrenceCount,
            VerseCount = bookRows.Sum(r => r.VerseCount),
            MappedCount = mapped,
            UnmappedCount = occurrenceCount - mapped,
            ByBook = bookRows.Select(r => new StrongsBookCountResponse { BibleBookId = r.BibleBookId, Count = r.Count, VerseCount = r.VerseCount }).ToList(),
            Renderings = renderings.Select(r => new StrongsRenderingResponse { Word = r.Word, Key = r.Key, Count = r.Count }).ToList()
        };

        if (entries.Count > 0)
        {
            response.Lemma = entries[0].OriginalWord;
            response.Transliteration = entries[0].Transliteration;
            response.Gloss = string.Join("; ", entries.Select(e => e.Gloss).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct());
        }

        if (keys.Length > 1)
        {
            var counts = (await _stats.GetKeyCountsAsync(keys, hebrew, token)).ToDictionary(r => r.Strongs, r => r.Count);
            response.Variants = keys.Select(key => ToVariant(key, entries, counts)).ToList();
        }

        return new StrongsSummaryResult(StrongsSummaryStatus.Ok, response);
    }

    private static string ResolveLanguage(bool hebrew, IReadOnlyList<LexiconEntry> entries, int aramaicCount, int occurrenceCount)
    {
        if (!hebrew)
        {
            return "greek";
        }

        // The lexicon LanguageId alone is unreliable (H0426 is Hebrew there, but all its words are Aramaic), so the grammar of the words wins.
        if (occurrenceCount > 0)
        {
            return aramaicCount * 2 > occurrenceCount ? "aramaic" : "hebrew";
        }

        return entries.Count > 0 && entries[0].LanguageId == (int)StrongsLanguage.Aramaic ? "aramaic" : "hebrew";
    }

    private static StrongsVariantResponse ToVariant(string key, IReadOnlyList<LexiconEntry> entries, Dictionary<string, int> counts)
    {
        var entry = entries.FirstOrDefault(e => Matches(key, e));
        return new StrongsVariantResponse
        {
            Strongs = key,
            Lemma = entry?.OriginalWord ?? string.Empty,
            Transliteration = entry?.Transliteration,
            Gloss = entry?.Gloss,
            Count = counts.GetValueOrDefault(key)
        };
    }

    private static bool Matches(string key, LexiconEntry entry)
    {
        var parsed = StrongsNumber.Parse(key).Value;
        return parsed is not null && parsed.Value.Number == entry.Number && (parsed.Value.Extra ?? string.Empty) == (entry.Extra ?? string.Empty);
    }
}
