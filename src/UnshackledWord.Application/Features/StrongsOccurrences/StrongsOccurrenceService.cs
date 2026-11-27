using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Application.Features.GrammarSearch;
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

    private readonly GrammarFilterResolver _resolver;

    public StrongsOccurrenceService(IStrongsLexiconRepository lexicon, IStrongsStatsRepository stats, ILxxStrongsOccurrenceRepository lxx,
        GrammarFilterResolver resolver)
    {
        _lexicon = lexicon;
        _stats = stats;
        _lxx = lxx;
        _resolver = resolver;
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

    public async Task<GetStrongsOccurrencesResponse?> GetAsync(string strongs, int page, int pageSize, StrongsOccurrenceFilter filter, CancellationToken token = default) =>
        (await GetAsync(strongs, page, pageSize, filter, null, token)).Response;

    public async Task<GrammarOutcome<GetStrongsOccurrencesResponse>> GetAsync(string strongs, int page, int pageSize, StrongsOccurrenceFilter filter, string? grammar, CancellationToken token = default)
    {
        if (!StrongsNumber.TryNormalize(strongs, out var normalized, out var hebrew) || page < 1 || pageSize < 1)
        {
            return new();
        }

        if (!string.IsNullOrWhiteSpace(grammar))
        {
            var codes = await ResolveCodesAsync(grammar, hebrew, token);
            if (codes.Error is not null) return new() { Error = codes.Error };
            filter = filter with { Codes = codes.Codes };
        }

        var parsed = StrongsNumber.Parse(normalized).Value!.Value;
        var entries = await _lexicon.GetEntriesAsync(parsed, token);
        var keys = StrongsMatchKeys.Resolve(normalized, parsed, entries);

        var total = await _stats.CountOccurrencesAsync(keys, hebrew, filter, token);
        var rows = total == 0 || (long)(page - 1) * pageSize >= total
            ? []
            : await _stats.GetOccurrencesAsync(keys, hebrew, filter, (page - 1) * pageSize, pageSize, token);

        return new()
        {
            Response = new GetStrongsOccurrencesResponse
            {
                Strongs = normalized,
                Language = hebrew ? "hebrew" : "greek",
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize),
                Items = rows.Select(ToResponse).ToList()
            }
        };
    }

    /// <summary>
    /// The <c>grammar</c> parameter (roadmap 0029.8): filter tokens only, read as one slot bound to the Strong's number, resolved to the code set of its language.
    /// A word token or a <c>dt.</c> filter is rejected; a key the language does not have is <c>keyNotInLanguage</c>.
    /// </summary>
    private async Task<(IReadOnlyList<string>? Codes, GrammarQueryError? Error)> ResolveCodesAsync(string grammar, bool hebrew, CancellationToken token)
    {
        var parsed = GrammarQueryParser.Parse(grammar);
        if (parsed.Error is not null) return (null, parsed.Error);

        var word = parsed.Slots.FirstOrDefault(s => s.Word is not null);
        if (word is not null) return (null, new GrammarQueryError(GrammarErrorCodes.UnknownKey, word.Text, GrammarVocabulary.KeyNames(false)));

        var filters = parsed.Slots.SelectMany(s => s.Filters).ToList();
        if (filters.FirstOrDefault(f => f.German) is { } german) return (null, new GrammarQueryError(GrammarErrorCodes.UnknownKey, german.Text, GrammarVocabulary.KeyNames(false)));

        if (GrammarSearchService.CheckKeysExist(filters, true, !hebrew, hebrew) is { } keyError) return (null, keyError);

        var codes = hebrew ? await _resolver.ResolveHebrewAsync(filters, token) : await _resolver.ResolveGreekAsync(filters, token);
        return (codes, null);
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
