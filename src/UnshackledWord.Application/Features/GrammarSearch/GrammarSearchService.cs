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

    /// <summary>The languages a slot is searched in: those of its lemmas, both without a lemma.</summary>
    private static (bool Greek, bool Hebrew) SlotLanguages(GrammarSlotSpec spec, IReadOnlyList<GrammarLemma> lemmas) =>
        spec.Word is null ? (true, true) : (lemmas.Any(l => !l.IsHebrew), lemmas.Any(l => l.IsHebrew));

    private static bool AppliesToGreek(GrammarKeyDef key) => key.Languages.HasFlag(GrammarLanguages.Greek);

    private static bool AppliesToHebrew(GrammarKeyDef key) => (key.Languages & GrammarLanguages.HebrewAramaic) != 0;

    private static bool FitsGreek(GrammarSlotSpec spec) => spec.Filters.All(f => AppliesToGreek(f.Key));

    private static bool FitsHebrew(GrammarSlotSpec spec) => spec.Filters.All(f => AppliesToHebrew(f.Key));

    /// <summary>A slot with a lemma whose filters do not all exist in at least one language of that lemma is an error (<c>G26 Stamm:qal</c>); without a lemma such a slot just has no hit in that language.</summary>
    private static GrammarQueryError? CheckKeysExist(GrammarSlotSpec spec, bool greek, bool hebrew)
    {
        if (spec.Word is null || (greek && FitsGreek(spec)) || (hebrew && FitsHebrew(spec))) return null;

        var offending = spec.Filters.FirstOrDefault(f => !(greek && AppliesToGreek(f.Key)) && !(hebrew && AppliesToHebrew(f.Key))) ?? spec.Filters[0];
        var candidates = GrammarVocabulary.Keys.Where(k => k.Kind != GrammarKeyKind.Word && ((greek && AppliesToGreek(k)) || (hebrew && AppliesToHebrew(k)))).Select(k => k.Name).ToList();
        return new GrammarQueryError(GrammarErrorCodes.KeyNotInLanguage, offending.Text, candidates);
    }

    private async Task<GrammarSide?> ResolveSideAsync(GrammarSlotSpec spec, IReadOnlyList<GrammarLemma> lemmas, bool hebrew, CancellationToken token)
    {
        var codes = spec.Filters.Count == 0 ? null : hebrew ? await _resolver.ResolveHebrewAsync(spec.Filters, token) : await _resolver.ResolveGreekAsync(spec.Filters, token);
        if (codes is { Count: 0 }) return null;

        return new GrammarSide(codes, spec.Word is null ? null : [.. lemmas.Where(l => l.IsHebrew == hebrew).SelectMany(l => StrongsKeys(l.Key))]);
    }

    /// <summary>Parses the query, reads the word tokens and resolves the filters of every slot (shared by the search and the chapter endpoints).</summary>
    private async Task<PreparedQuery> PrepareAsync(string? query, CancellationToken token)
    {
        var parsed = GrammarQueryParser.Parse(query);
        if (parsed.Error is not null) return new PreparedQuery([], parsed.Error);

        var slots = new List<ReadSlot>();
        foreach (var spec in parsed.Slots)
        {
            var lemmas = spec.Word is null ? [] : await _words.ReadAsync(spec.Word, spec.Mode, token);
            if (spec.Word is not null && lemmas.Count == 0) return new PreparedQuery([], new GrammarQueryError(GrammarErrorCodes.UnknownWord, spec.Word));

            var (greek, hebrew) = SlotLanguages(spec, lemmas);
            if (CheckKeysExist(spec, greek, hebrew) is { } keyError) return new PreparedQuery([], keyError);

            var greekSide = greek && FitsGreek(spec) ? await ResolveSideAsync(spec, lemmas, false, token) : null;
            var hebrewSide = hebrew && FitsHebrew(spec) ? await ResolveSideAsync(spec, lemmas, true, token) : null;
            slots.Add(new ReadSlot(spec, lemmas, greekSide is null && hebrewSide is null ? null : new GrammarSlotQuery(greekSide, hebrewSide)));
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
        var mappings = await ReadMappingsAsync(hits.SelectMany(v => v), token);

        response.Verses = [.. hits.OrderBy(v => v.Key).Select(v =>
        {
            var ids = v.Select(m => m.WordId).Distinct().Order().ToList();
            return new GrammarChapterVerse
            {
                Verse = v.Key % 1000,
                OriginalWordIds = ids,
                GermanPositions = [.. ids.SelectMany(id => mappings[(v.First().IsHebrew, id)]).Distinct().Order()],
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

    public async Task<GrammarOutcome<SearchGrammarFacetsResponse>> FacetsAsync(string? query, int slot, string? key, CancellationToken token = default)
    {
        var parsed = GrammarQueryParser.Parse(query, allowEmptyValue: true);
        if (parsed.Error is not null) return new() { Error = parsed.Error };
        if (slot < 0 || slot >= parsed.Slots.Count) return new() { Error = new GrammarQueryError(GrammarErrorCodes.UnknownSlot, slot.ToString(CultureInfo.InvariantCulture)) };

        var spec = parsed.Slots[slot];
        IReadOnlyList<GrammarLemma> lemmas = [];
        if (spec.Word is not null)
        {
            lemmas = await _words.ReadAsync(spec.Word, spec.Mode, token);
            if (lemmas.Count == 0) return new() { Error = new GrammarQueryError(GrammarErrorCodes.UnknownWord, spec.Word) };
        }

        var (greek, hebrew) = SlotLanguages(spec, lemmas);
        if (CheckKeysExist(spec, greek, hebrew) is { } keyError) return new() { Error = keyError };

        var allKeys = GrammarFacetFolder.FacetKeys(GrammarLanguages.Greek).Concat(GrammarFacetFolder.FacetKeys(GrammarLanguages.Hebrew)).Distinct().ToList();
        var facetKeys = allKeys.Where(k => (greek && AppliesToGreek(k)) || (hebrew && AppliesToHebrew(k))).ToList();
        if (!string.IsNullOrWhiteSpace(key))
        {
            var found = GrammarVocabulary.FindKey(key, false);
            if (found is null || !allKeys.Contains(found)) return new() { Error = new GrammarQueryError(GrammarErrorCodes.UnknownKey, key, [.. facetKeys.Select(k => k.Name)]) };
            if (!facetKeys.Contains(found)) return new() { Error = new GrammarQueryError(GrammarErrorCodes.KeyNotInLanguage, key, [.. facetKeys.Select(k => k.Name)]) };
            facetKeys = [found];
        }

        // One count per code and language for the lemma; every key then drops its own filters and folds the codes the other filters leave.
        var sides = new List<(string Language, Dictionary<string, int> Counts, IReadOnlyList<MorphologyCodeEntry> Index)>();
        if (greek)
        {
            var rows = await _repository.CountCodesAsync(spec.Word is null ? null : [.. lemmas.Where(l => !l.IsHebrew).SelectMany(l => StrongsKeys(l.Key))], token);
            sides.Add((GrammarVocabulary.LanguageGreek, rows.ToDictionary(r => r.Code, r => r.Count), await _resolver.GreekCodesAsync(token)));
        }

        if (hebrew)
        {
            var rows = await _repository.CountHebrewCodesAsync(spec.Word is null ? null : [.. lemmas.Where(l => l.IsHebrew).SelectMany(l => StrongsKeys(l.Key))], token);
            sides.Add((GrammarVocabulary.LanguageHebrew, rows.ToDictionary(r => r.Code, r => r.Count), await _resolver.HebrewCodesAsync(token)));
        }

        var response = new SearchGrammarFacetsResponse { Slot = slot };
        foreach (var facetKey in facetKeys)
        {
            var others = spec.Filters.Where(f => f.Key != facetKey).ToList();
            var perValue = new Dictionary<string, int>();
            foreach (var (language, counts, index) in sides)
            {
                if (!(language == GrammarVocabulary.LanguageGreek ? AppliesToGreek(facetKey) : AppliesToHebrew(facetKey))) continue;
                var allowedCodes = GrammarFilterResolver.Resolve(others, index, language).ToHashSet();
                foreach (var value in GrammarFacetFolder.Fold(facetKey, counts, index.Where(e => allowedCodes.Contains(e.Code))).Values)
                {
                    perValue[value.Key] = perValue.GetValueOrDefault(value.Key) + value.Count;
                }
            }

            if (perValue.Count == 0) continue;
            response.Facets.Add(new GrammarFacet
            {
                Key = facetKey.Name,
                Values = [.. facetKey.Values.Where(v => perValue.ContainsKey(v.Id)).Select(v => new GrammarFacetValue { Key = v.Id, LabelDe = v.De, Count = perValue[v.Id] })],
            });
        }

        return new() { Response = response };
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

    /// <summary>German positions per original word, keyed by (is Hebrew, word id) because the Greek and Hebrew word ids overlap.</summary>
    private async Task<ILookup<(bool IsHebrew, int WordId), int>> ReadMappingsAsync(IEnumerable<GrammarMatchRow> matches, CancellationToken token)
    {
        var rows = matches.ToList();
        var greek = await _repository.ReadMappingsAsync([.. rows.Where(m => !m.IsHebrew).Select(m => m.WordId).Distinct()], token);
        var hebrewIds = rows.Where(m => m.IsHebrew).Select(m => m.WordId).Distinct().ToList();
        var hebrew = hebrewIds.Count == 0 ? [] : await _repository.ReadHebrewMappingsAsync(hebrewIds, token);
        return greek.Select(m => (Key: (false, m.StepWordId), m.PositionInVerse))
            .Concat(hebrew.Select(m => (Key: (true, m.StepWordId), m.PositionInVerse)))
            .ToLookup(x => x.Key, x => x.PositionInVerse);
    }

    private async Task<List<GrammarHit>> BuildHitsAsync(IReadOnlyList<GrammarSlotQuery> queries, IReadOnlyList<int> verseIds, CancellationToken token)
    {
        var matches = await _repository.ReadMatchesAsync(queries, verseIds, token);
        var german = await _repository.ReadGermanWordsAsync(verseIds, token);
        var mappings = await ReadMappingsAsync(matches, token);
        var hebrewLemmas = await _words.ReadHebrewLemmasAsync(matches.Where(m => m.IsHebrew).Select(m => BaseKey(m.Strongs)).Distinct(), token);

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
                GermanPositions = [.. mappings[(m.IsHebrew, m.WordId)].Distinct().Order()],
                Original = ToOriginal(m, hebrewLemmas),
            })],
        })];
    }

    private static GrammarOriginalWord ToOriginal(GrammarMatchRow row, IReadOnlyDictionary<string, string> hebrewLemmas)
    {
        if (!row.IsHebrew)
        {
            return new GrammarOriginalWord
            {
                Language = GrammarVocabulary.LanguageGreek,
                Form = row.Form,
                Lemma = row.Lemma,
                Strongs = DisplayStrongs(row.Strongs),
                Code = row.Morphology,
                Keys = KeysOf(GreekMorphologyDecoder.Decode(row.Morphology)),
            };
        }

        var decoded = HebrewMorphologyDecoder.Decode(row.Morphology);
        return new GrammarOriginalWord
        {
            Language = decoded.IsAramaic ? GrammarVocabulary.LanguageAramaic : GrammarVocabulary.LanguageHebrew,
            Form = row.Form,
            Lemma = hebrewLemmas.GetValueOrDefault(BaseKey(row.Strongs), string.Empty),
            Strongs = DisplayStrongs(row.Strongs),
            Code = row.Morphology,
            Keys = KeysOf(decoded.Root),
        };
    }

    /// <summary>"H1254A" → "H1254": the lexicon key of a stored Strong's key.</summary>
    private static string BaseKey(string key) => key.Length > 5 ? key[..5] : key;

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
        Add("stem", keys.Stem);
        Add("hebrewForm", keys.HebrewForm);
        Add("state", keys.State);
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
