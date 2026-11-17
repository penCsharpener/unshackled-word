using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;

namespace UnshackledWord.Application.Features.StrongsStats;

public sealed class StrongsSummaryService : IStrongsSummaryService
{
    private readonly IStrongsLexiconRepository _lexicon;
    private readonly IStrongsStatsRepository _stats;
    private readonly IStatsRefreshStatus _refreshStatus;

    public StrongsSummaryService(IStrongsLexiconRepository lexicon, IStrongsStatsRepository stats, IStatsRefreshStatus refreshStatus)
    {
        _lexicon = lexicon;
        _stats = stats;
        _refreshStatus = refreshStatus;
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

        var statsRefreshedAt = await _stats.GetSnapshotRefreshedAtAsync(token);
        if (statsRefreshedAt is not null)
        {
            return await GetFromSnapshotAsync(normalized, hebrew, keys, entries, statsRefreshedAt.Value, token);
        }

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
            Renderings = renderings.Select(r => new StrongsRenderingResponse { Word = r.Word, Key = r.Key, Count = r.Count }).ToList(),
            StatsRefreshing = _refreshStatus.IsRunning
        };

        ApplyLexicon(response, entries);

        if (keys.Length > 1)
        {
            var counts = (await _stats.GetKeyCountsAsync(keys, hebrew, token)).ToDictionary(r => r.Strongs, r => r.Count);
            response.Variants = keys.Select(key => ToVariant(key, entries, counts)).ToList();
        }

        return new StrongsSummaryResult(StrongsSummaryStatus.Ok, response);
    }

    /// <summary>Snapshot path: counts and renderings (German lemma + PoS) from the Stats* views, summed over the match keys.</summary>
    private async Task<StrongsSummaryResult> GetFromSnapshotAsync(string normalized, bool hebrew, string[] keys, IReadOnlyList<LexiconEntry> entries, DateTime statsRefreshedAt, CancellationToken token)
    {
        var strongsRows = await _stats.GetSnapshotStrongsAsync(keys, hebrew, token);
        var occurrenceCount = strongsRows.Sum(r => r.Occurrences);
        if (entries.Count == 0 && occurrenceCount == 0)
        {
            return new StrongsSummaryResult(StrongsSummaryStatus.NotFound);
        }

        var books = SnapshotBookCounts.Merge(strongsRows.Select(r => r.BookCounts)).Where(b => b.B > 0).ToList();
        var aramaicCount = hebrew && occurrenceCount > 0 ? await _stats.GetAramaicCountAsync(keys, token) : 0;
        IReadOnlyList<SnapshotRenderingRow> renderings = occurrenceCount == 0 ? [] : await _stats.GetSnapshotRenderingsAsync(keys, hebrew, token);
        var mapped = strongsRows.Sum(r => r.MappedCount);

        var response = new GetStrongsSummaryResponse
        {
            Strongs = normalized,
            Language = ResolveLanguage(hebrew, entries, aramaicCount, occurrenceCount),
            OccurrenceCount = occurrenceCount,
            VerseCount = books.Sum(b => b.V),
            MappedCount = mapped,
            UnmappedCount = occurrenceCount - mapped,
            ByBook = books.Select(b => new StrongsBookCountResponse { BibleBookId = b.B, Count = b.C, VerseCount = b.V }).ToList(),
            Renderings = renderings.Select(r => new StrongsRenderingResponse { Word = r.Word, Key = r.Key, Pos = r.Pos, Count = r.Count, NoOwnLemma = r.NoOwnLemma }).ToList(),
            StatsRefreshedAt = statsRefreshedAt,
            LinkRolesAppliedAt = await _stats.GetLinkRolesAppliedAtAsync(token),
            StatsRefreshing = _refreshStatus.IsRunning
        };

        ApplyLexicon(response, entries);

        if (keys.Length > 1)
        {
            var counts = strongsRows.ToDictionary(r => r.Strongs, r => r.Occurrences);
            response.Variants = keys.Select(key => ToVariant(key, entries, counts)).ToList();
        }

        return new StrongsSummaryResult(StrongsSummaryStatus.Ok, response);
    }

    private static void ApplyLexicon(GetStrongsSummaryResponse response, IReadOnlyList<LexiconEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        response.Lemma = entries[0].OriginalWord;
        response.Transliteration = entries[0].Transliteration;
        response.Gloss = string.Join("; ", entries.Select(e => e.Gloss).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct());
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
