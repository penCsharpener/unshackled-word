using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

namespace UnshackledWord.Application.Features.StrongsOccurrences;

public sealed class StrongsOccurrenceService : IStrongsOccurrenceService
{
    private readonly IStrongsLexiconRepository _lexicon;
    private readonly IStrongsStatsRepository _stats;
    private readonly ILxxStrongsOccurrenceRepository _lxx;

    public StrongsOccurrenceService(IStrongsLexiconRepository lexicon, IStrongsStatsRepository stats, ILxxStrongsOccurrenceRepository lxx)
    {
        _lexicon = lexicon;
        _stats = stats;
        _lxx = lxx;
    }

    public async Task<GetStrongsOccurrencesResponse?> GetLxxAsync(string strongs, int page, int pageSize, bool includeUnconfirmedCount, CancellationToken token = default)
    {
        if (!StrongsNumber.TryNormalize(strongs, out var normalized, out var hebrew) || hebrew || page < 1 || pageSize < 1)
        {
            return null;
        }

        var parsed = StrongsNumber.Parse(normalized).Value!.Value;
        var entries = await _lexicon.GetEntriesAsync(parsed, token);
        var keys = StrongsMatchKeys.Resolve(normalized, parsed, entries);

        var counts = await _lxx.CountAsync(keys, token);
        var rows = counts.Confirmed == 0 || (long)(page - 1) * pageSize >= counts.Confirmed
            ? []
            : await _lxx.GetConfirmedPageAsync(keys, (page - 1) * pageSize, pageSize, token);

        return new GetStrongsOccurrencesResponse
        {
            Strongs = normalized,
            Language = "greek",
            Corpus = "lxx",
            Page = page,
            PageSize = pageSize,
            TotalItems = counts.Confirmed,
            TotalPages = (int)Math.Ceiling((double)counts.Confirmed / pageSize),
            LxxItems = rows.Select(ToLxxResponse).ToList(),
            UnconfirmedCount = includeUnconfirmedCount ? counts.Proposed : null
        };
    }

    public static LxxStrongsOccurrenceResponse ToLxxResponse(LxxStrongsOccurrenceRow row)
    {
        var item = new LxxStrongsOccurrenceResponse
        {
            LxxWordId = row.LxxWordId,
            LxxVerseId = row.LxxVerseId,
            Ref = LxxChapterReadService.FormatRef(row.BibleBookId, row.Chapter, row.Verse, row.Subverse),
            TextForm = row.TextForm,
            BibleBookId = row.BibleBookId,
            Chapter = row.Chapter,
            Verse = row.Verse,
            Subverse = row.Subverse,
            Words = row.Words.OrderBy(w => w.Position).Select(w => Token(w.Greek, w.Position == row.Position)).ToList()
        };

        if (row.ElbHebRefId is { } heb)
        {
            item.Elb = new LxxElbVerseRef
            {
                HebRefId = heb,
                BibleBookId = heb / 1_000_000,
                Chapter = heb / 1_000 % 1_000,
                Verse = heb % 1_000,
                Text = row.ElbText,
                ElbIsAnchor = row.ElbIsAnchor
            };
        }

        return item;
    }

    public async Task<GetStrongsOccurrencesResponse?> GetAsync(string strongs, int page, int pageSize, StrongsOccurrenceFilter filter, CancellationToken token = default)
    {
        if (!StrongsNumber.TryNormalize(strongs, out var normalized, out var hebrew) || page < 1 || pageSize < 1)
        {
            return null;
        }

        var parsed = StrongsNumber.Parse(normalized).Value!.Value;
        var entries = await _lexicon.GetEntriesAsync(parsed, token);
        var keys = StrongsMatchKeys.Resolve(normalized, parsed, entries);

        var total = await _stats.CountOccurrencesAsync(keys, hebrew, filter, token);
        var rows = total == 0 || (long)(page - 1) * pageSize >= total
            ? []
            : await _stats.GetOccurrencesAsync(keys, hebrew, filter, (page - 1) * pageSize, pageSize, token);

        return new GetStrongsOccurrencesResponse
        {
            Strongs = normalized,
            Language = hebrew ? "hebrew" : "greek",
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            Items = rows.Select(ToResponse).ToList()
        };
    }

    public static StrongsOccurrenceResponse ToResponse(StrongsOccurrenceRow row)
    {
        var item = new StrongsOccurrenceResponse
        {
            BibleBookId = row.BibleBookId,
            Chapter = row.Chapter,
            Verse = row.Verse,
            Original = row.Original
        };

        var originalWords = row.OriginalWords ?? [];
        var originalPositions = row.OriginalPositions ?? [];
        for (var i = 0; i < Math.Min(originalWords.Length, originalPositions.Length); i++)
        {
            item.OriginalVerse.Add(Token(originalWords[i], originalPositions[i] == row.OriginalPosition));
        }

        var positions = row.VersePositions ?? [];
        var words = row.VerseWords ?? [];
        var count = Math.Min(positions.Length, words.Length);
        var matched = row.MatchedPositions is { Length: > 0 } m ? m.ToHashSet() : [];

        if (matched.Count == 0)
        {
            item.After = Enumerable.Range(0, count).Select(i => Token(words[i], false)).ToList();
            return item;
        }

        var first = matched.Min();
        var last = matched.Max();
        for (var i = 0; i < count; i++)
        {
            if (positions[i] < first)
            {
                item.Before.Add(Token(words[i], false));
            }
            else if (positions[i] > last)
            {
                item.After.Add(Token(words[i], false));
            }
            else
            {
                item.Keyword.Add(Token(words[i], matched.Contains(positions[i])));
            }
        }

        if (item.Keyword.Count > 0)
        {
            var text = item.Keyword[^1].Text;
            var core = text.TrimEnd(text.Reverse().TakeWhile(c => !char.IsLetterOrDigit(c)).ToArray());
            if (core.Length > 0 && core.Length < text.Length)
            {
                item.Keyword[^1].Text = core;
                item.KeywordSuffix = text[core.Length..];
            }
        }

        return item;
    }

    private static StrongsOccurrenceTokenResponse Token(string text, bool matched)
    {
        return new StrongsOccurrenceTokenResponse { Text = text, Matched = matched };
    }
}
