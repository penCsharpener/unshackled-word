using System.Globalization;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>
/// Grammar search (roadmap 0029.1): parses the query, reads word tokens as lemmas, resolves the filters of every slot to Greek code sets, finds the verses where
/// every slot has its own word and answers one page of them with the Elb1871 words and the matched original words.
/// </summary>
public sealed class GrammarSearchService : IGrammarSearchService
{
    private const int VerseChunk = 2000;
    private const int BookShift = 1_000_000;

    private readonly IGrammarWordReader _words;
    private readonly GrammarFilterResolver _resolver;
    private readonly IGrammarSearchRepository _repository;

    public GrammarSearchService(IGrammarWordReader words, GrammarFilterResolver resolver, IGrammarSearchRepository repository)
    {
        _words = words;
        _resolver = resolver;
        _repository = repository;
    }

    private sealed record ReadSlot(GrammarSlotSpec Spec, IReadOnlyList<GrammarLemma> Lemmas, GrammarSlotQuery? Query);

    public async Task<GrammarSearchResult> SearchAsync(string? query, int page, int? bookId, CancellationToken token = default)
    {
        var parsed = GrammarQueryParser.Parse(query);
        if (parsed.Error is not null) return new GrammarSearchResult { Error = parsed.Error };

        var slots = new List<ReadSlot>();
        foreach (var spec in parsed.Slots)
        {
            var lemmas = spec.Word is null ? [] : await _words.ReadGreekAsync(spec.Word, spec.Mode, token);
            if (spec.Word is not null && lemmas.Count == 0) return new GrammarSearchResult { Error = new GrammarQueryError(GrammarErrorCodes.UnknownWord, spec.Word) };

            var codes = spec.Filters.Count == 0 ? null : await _resolver.ResolveGreekAsync(spec.Filters, token);
            var slotQuery = codes is { Count: 0 }
                ? null
                : new GrammarSlotQuery(codes, spec.Word is null ? null : [.. lemmas.SelectMany(l => StrongsKeys(l.Key))]);
            slots.Add(new ReadSlot(spec, lemmas, slotQuery));
        }

        var response = new SearchGrammarResponse
        {
            Slots = [.. slots.Select(ToSlotResponse)],
            Page = page,
            PageSize = SearchGrammarRequest.PageSize,
        };

        // A slot without any matching code can have no hit: no query needed.
        if (slots.Any(s => s.Query is null)) return new GrammarSearchResult { Response = response };

        var queries = slots.Select(s => s.Query!).ToList();
        var verses = await FindHitVersesAsync(queries, token);
        response.TotalVerses = verses.Count;
        response.TotalWords = verses.Sum(v => v.Words);
        response.Books = [.. verses.GroupBy(v => v.HebRefId / BookShift).OrderBy(g => g.Key).Select(g => new GrammarBookCount { BibleBookId = g.Key, Count = g.Count() })];

        var inBook = bookId is null or 0 ? verses : [.. verses.Where(v => v.HebRefId / BookShift == bookId)];
        var pageIds = inBook.Skip((Math.Max(page, 1) - 1) * SearchGrammarRequest.PageSize).Take(SearchGrammarRequest.PageSize).Select(v => v.HebRefId).ToList();
        if (pageIds.Count > 0) response.Hits = await BuildHitsAsync(queries, pageIds, token);

        return new GrammarSearchResult { Response = response };
    }

    /// <summary>The hit verses in canonical order; verses where two slots share a candidate word are kept only when each slot can get its own word.</summary>
    private async Task<List<GrammarVerseRow>> FindHitVersesAsync(IReadOnlyList<GrammarSlotQuery> queries, CancellationToken token)
    {
        var rows = await _repository.FindVersesAsync(queries, token);
        var shared = rows.Where(r => r.Shared).Select(r => r.HebRefId).ToList();
        var rejected = new HashSet<int>();
        foreach (var chunk in shared.Chunk(VerseChunk))
        {
            var matches = await _repository.ReadMatchesAsync(queries, chunk, token);
            foreach (var verse in matches.GroupBy(m => m.HebRefId))
            {
                var perSlot = Enumerable.Range(0, queries.Count)
                    .Select(i => verse.Where(m => m.Slot == i).Select(m => m.WordId).ToHashSet())
                    .ToList();
                if (!DistinctWordAssignment.Exists(perSlot)) rejected.Add(verse.Key);
            }
        }

        return [.. rows.Where(r => !rejected.Contains(r.HebRefId)).OrderBy(r => r.HebRefId)];
    }

    private async Task<List<GrammarHit>> BuildHitsAsync(IReadOnlyList<GrammarSlotQuery> queries, IReadOnlyList<int> verseIds, CancellationToken token)
    {
        var matches = await _repository.ReadMatchesAsync(queries, verseIds, token);
        var german = await _repository.ReadGermanWordsAsync(verseIds, token);
        var mappings = (await _repository.ReadMappingsAsync([.. matches.Select(m => m.WordId).Distinct()], token))
            .ToLookup(m => m.StepWordId, m => m.PositionInVerse);

        var matchesByVerse = matches.ToLookup(m => m.HebRefId);
        var germanByVerse = german.ToLookup(g => g.HebRefId);
        return [.. verseIds.Select(id => new GrammarHit
        {
            BibleBookId = id / BookShift,
            Chapter = id / 1000 % 1000,
            Verse = id % 1000,
            Words = [.. germanByVerse[id].OrderBy(w => w.PositionInVerse).Select(w => new GrammarHitWord { Text = w.WordInContext, Position = w.PositionInVerse })],
            Matches = [.. matchesByVerse[id].OrderBy(m => m.Slot).ThenBy(m => m.Position).Select(m => new GrammarMatch
            {
                Slot = m.Slot,
                GermanPositions = [.. mappings[m.WordId].Distinct().Order()],
                Original = ToOriginal(m),
            })],
        })];
    }

    private static GrammarOriginalWord ToOriginal(GrammarMatchRow row) => new()
    {
        Form = row.Greek,
        Lemma = row.Lemma,
        Strongs = DisplayStrongs(row.Strongs),
        Code = row.Morphology,
        Keys = KeysOf(GreekMorphologyDecoder.Decode(row.Morphology)),
    };

    private static Dictionary<string, string> KeysOf(MorphologyKeys keys)
    {
        var result = new Dictionary<string, string>();
        void Add(string name, string? value)
        {
            if (value is not null) result[name] = value;
        }

        Add("partOfSpeech", keys.PartOfSpeech);
        Add("tense", keys.Tense);
        Add("voice", keys.Voice);
        Add("mood", keys.Mood);
        Add("person", keys.Person);
        Add("case", keys.Case);
        Add("number", keys.Number);
        Add("gender", keys.Gender);
        Add("degree", keys.Degree);
        return result;
    }

    private static GrammarSlotResponse ToSlotResponse(ReadSlot slot) => new()
    {
        Text = slot.Spec.Text,
        ReadAs = slot.Spec.Word is null ? "any" : "lemma",
        Lemmas = [.. slot.Lemmas.Select(l => new GrammarLemmaResponse { Strongs = DisplayStrongs(l.Key), Lemma = l.Lemma, Transliteration = l.Transliteration, Gloss = l.Gloss })],
        Filters = [.. slot.Spec.Filters.Select(f => new GrammarFilterResponse
        {
            Key = f.Key.Name,
            Values = f.Key.Kind == GrammarKeyKind.Code ? [.. f.CodePatterns] : [.. f.Values.Select(v => v.De)],
            Negated = f.Negated,
            German = f.German,
        })],
    };

    /// <summary>All stored spellings of a base key: "G0026" is stored as "G0026" or, for lexicon variants, "G0026A" … "G0026Z".</summary>
    public static IEnumerable<string> StrongsKeys(string baseKey) =>
        new[] { baseKey }.Concat(Enumerable.Range('A', 26).Select(c => baseKey + (char)c));

    /// <summary>"G0026G" → "G26".</summary>
    public static string DisplayStrongs(string key)
    {
        if (key.Length < 5 || !int.TryParse(key.AsSpan(1, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return key;
        return key[..1] + number.ToString(CultureInfo.InvariantCulture);
    }
}
