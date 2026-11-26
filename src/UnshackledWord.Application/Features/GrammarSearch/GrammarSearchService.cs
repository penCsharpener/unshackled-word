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
    private const int ChapterSpan = 1000;
    private const int NeighbourBatch = 50;

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

    private sealed record PreparedQuery(List<ReadSlot> Slots, GrammarQueryError? Error)
    {
        /// <summary>A slot without any matching code can have no hit: no query needed.</summary>
        public bool CanHit => Error is null && Slots.All(s => s.Query is not null);

        public List<GrammarSlotQuery> Queries => [.. Slots.Select(s => s.Query!)];
    }

    /// <summary>Parses the query, reads the word tokens and resolves the filters of every slot (shared by the search and the chapter endpoints).</summary>
    private async Task<PreparedQuery> PrepareAsync(string? query, CancellationToken token)
    {
        var parsed = GrammarQueryParser.Parse(query);
        if (parsed.Error is not null) return new PreparedQuery([], parsed.Error);

        var slots = new List<ReadSlot>();
        foreach (var spec in parsed.Slots)
        {
            var lemmas = spec.Word is null ? [] : await _words.ReadGreekAsync(spec.Word, spec.Mode, token);
            if (spec.Word is not null && lemmas.Count == 0) return new PreparedQuery([], new GrammarQueryError(GrammarErrorCodes.UnknownWord, spec.Word));

            var codes = spec.Filters.Count == 0 ? null : await _resolver.ResolveGreekAsync(spec.Filters, token);
            var slotQuery = codes is { Count: 0 }
                ? null
                : new GrammarSlotQuery(codes, spec.Word is null ? null : [.. lemmas.SelectMany(l => StrongsKeys(l.Key))]);
            slots.Add(new ReadSlot(spec, lemmas, slotQuery));
        }

        return new PreparedQuery(slots, null);
    }

    public async Task<GrammarSearchResult> SearchAsync(string? query, int page, int? bookId, CancellationToken token = default)
    {
        var prepared = await PrepareAsync(query, token);
        if (prepared.Error is not null) return new GrammarSearchResult { Error = prepared.Error };
        var slots = prepared.Slots;

        var response = new SearchGrammarResponse
        {
            Slots = [.. slots.Select(ToSlotResponse)],
            Page = page,
            PageSize = SearchGrammarRequest.PageSize,
        };

        if (!prepared.CanHit) return new GrammarSearchResult { Response = response };

        var queries = prepared.Queries;
        var verses = await FindHitVersesAsync(queries, token);
        response.TotalVerses = verses.Count;
        response.TotalWords = verses.Sum(v => v.Words);
        response.Books = [.. verses.GroupBy(v => v.HebRefId / BookShift).OrderBy(g => g.Key).Select(g => new GrammarBookCount { BibleBookId = g.Key, Count = g.Count() })];

        var inBook = bookId is null or 0 ? verses : [.. verses.Where(v => v.HebRefId / BookShift == bookId)];
        var pageIds = inBook.Skip((Math.Max(page, 1) - 1) * SearchGrammarRequest.PageSize).Take(SearchGrammarRequest.PageSize).Select(v => v.HebRefId).ToList();
        if (pageIds.Count > 0) response.Hits = await BuildHitsAsync(queries, pageIds, token);

        return new GrammarSearchResult { Response = response };
    }

    public async Task<GrammarOutcome<SearchGrammarChapterResponse>> ChapterAsync(string? query, int bibleBookId, int chapter, CancellationToken token = default)
    {
        var prepared = await PrepareAsync(query, token);
        if (prepared.Error is not null) return new() { Error = prepared.Error };

        var response = new SearchGrammarChapterResponse();
        if (!prepared.CanHit) return new() { Response = response };

        var queries = prepared.Queries;
        var start = ChapterStart(bibleBookId, chapter);
        var matches = await _repository.ReadMatchesInRangeAsync(queries, start, start + ChapterSpan - 1, token);

        // Keep the verses where every slot can get its own word, then mark all candidate words of those verses.
        var hits = matches.GroupBy(m => m.HebRefId)
            .Where(v => HasAssignment(v, queries.Count))
            .ToList();
        var wordIds = hits.SelectMany(v => v).Select(m => m.WordId).Distinct().ToList();
        var mappings = (await _repository.ReadMappingsAsync(wordIds, token)).ToLookup(m => m.StepWordId, m => m.PositionInVerse);

        response.Verses = [.. hits.OrderBy(v => v.Key).Select(v =>
        {
            var ids = v.Select(m => m.WordId).Distinct().Order().ToList();
            return new GrammarChapterVerse
            {
                Verse = v.Key % 1000,
                OriginalWordIds = ids,
                GermanPositions = [.. ids.SelectMany(id => mappings[id]).Distinct().Order()],
            };
        })];
        response.Total = wordIds.Count;
        return new() { Response = response };
    }

    public async Task<GrammarOutcome<SearchGrammarNeighboursResponse>> NeighboursAsync(string? query, int bibleBookId, int chapter, CancellationToken token = default)
    {
        var prepared = await PrepareAsync(query, token);
        if (prepared.Error is not null) return new() { Error = prepared.Error };

        var response = new SearchGrammarNeighboursResponse();
        if (!prepared.CanHit) return new() { Response = response };

        var queries = prepared.Queries;
        var start = ChapterStart(bibleBookId, chapter);
        response.Previous = await FindNearestChapterAsync(queries, start, true, token);
        response.Next = await FindNearestChapterAsync(queries, start + ChapterSpan - 1, false, token);
        return new() { Response = response };
    }

    /// <summary>The chapter of the nearest hit verse beyond the boundary; verses are fetched in small batches because a verse whose slots share a word can still be rejected.</summary>
    private async Task<GrammarChapterRef?> FindNearestChapterAsync(IReadOnlyList<GrammarSlotQuery> queries, int boundary, bool before, CancellationToken token)
    {
        while (true)
        {
            var rows = await _repository.FindVersesNearAsync(queries, boundary, before, NeighbourBatch, token);
            if (rows.Count == 0) return null;

            var rejected = await RejectedVersesAsync(queries, rows, token);
            var hit = rows.FirstOrDefault(r => !rejected.Contains(r.HebRefId));
            if (hit is not null) return new GrammarChapterRef { BibleBookId = hit.HebRefId / BookShift, Chapter = hit.HebRefId / ChapterSpan % 1000 };

            if (rows.Count < NeighbourBatch) return null;
            boundary = rows[^1].HebRefId;
        }
    }

    private static int ChapterStart(int bibleBookId, int chapter) => bibleBookId * BookShift + chapter * ChapterSpan;

    private static bool HasAssignment(IEnumerable<GrammarMatchRow> verseMatches, int slotCount)
    {
        var rows = verseMatches.ToList();
        var perSlot = Enumerable.Range(0, slotCount).Select(i => rows.Where(m => m.Slot == i).Select(m => m.WordId).ToHashSet()).ToList();
        return perSlot.All(p => p.Count > 0) && DistinctWordAssignment.Exists(perSlot);
    }

    /// <summary>Of the given verses, those where two slots share a candidate and no distinct assignment exists.</summary>
    private async Task<HashSet<int>> RejectedVersesAsync(IReadOnlyList<GrammarSlotQuery> queries, IEnumerable<GrammarVerseRow> rows, CancellationToken token)
    {
        var rejected = new HashSet<int>();
        foreach (var chunk in rows.Where(r => r.Shared).Select(r => r.HebRefId).Chunk(VerseChunk))
        {
            var matches = await _repository.ReadMatchesAsync(queries, chunk, token);
            foreach (var verse in matches.GroupBy(m => m.HebRefId))
            {
                if (!HasAssignment(verse, queries.Count)) rejected.Add(verse.Key);
            }
        }

        return rejected;
    }

    /// <summary>The hit verses in canonical order; verses where two slots share a candidate word are kept only when each slot can get its own word.</summary>
    private async Task<List<GrammarVerseRow>> FindHitVersesAsync(IReadOnlyList<GrammarSlotQuery> queries, CancellationToken token)
    {
        var rows = await _repository.FindVersesAsync(queries, token);
        var rejected = await RejectedVersesAsync(queries, rows, token);
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
