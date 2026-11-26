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

    /// <param name="GermanLemma">The lower-case German lemma when the word token was read as a German word (roadmap 0029.7); then <paramref name="Lemmas"/> is empty.</param>
    private sealed record ReadSlot(GrammarSlotSpec Spec, IReadOnlyList<GrammarLemma> Lemmas, string? GermanLemma, GrammarSlotQuery? Query);

    private sealed record PreparedQuery(List<ReadSlot> Slots, GrammarQueryError? Error)
    {
        /// <summary>A slot without any matching code can have no hit: no query needed.</summary>
        public bool CanHit => Error is null && Slots.All(s => s.Query is not null);

        public List<GrammarSlotQuery> Queries => [.. Slots.Select(s => s.Query!)];
    }

    /// <summary>The languages a slot is searched in: those of its lemmas, both without an original lemma.</summary>
    private static (bool Greek, bool Hebrew) SlotLanguages(IReadOnlyList<GrammarLemma> lemmas) =>
        lemmas.Count == 0 ? (true, true) : (lemmas.Any(l => !l.IsHebrew), lemmas.Any(l => l.IsHebrew));

    private static bool AppliesToGreek(GrammarKeyDef key) => key.Languages.HasFlag(GrammarLanguages.Greek);

    private static bool AppliesToHebrew(GrammarKeyDef key) => (key.Languages & GrammarLanguages.HebrewAramaic) != 0;

    private static List<GrammarFilter> OriginalFilters(GrammarSlotSpec spec) => [.. spec.Filters.Where(f => !f.German)];

    private static List<GrammarFilter> GermanFilters(GrammarSlotSpec spec) => [.. spec.Filters.Where(f => f.German)];

    private static bool FitsGreek(IReadOnlyList<GrammarFilter> filters) => filters.All(f => AppliesToGreek(f.Key));

    private static bool FitsHebrew(IReadOnlyList<GrammarFilter> filters) => filters.All(f => AppliesToHebrew(f.Key));

    /// <summary>A slot with an original lemma whose filters do not all exist in at least one language of that lemma is an error (<c>G26 Stamm:qal</c>); without a lemma such a slot just has no hit in that language.</summary>
    private static GrammarQueryError? CheckKeysExist(IReadOnlyList<GrammarFilter> filters, bool hasLemma, bool greek, bool hebrew)
    {
        if (!hasLemma || filters.Count == 0 || (greek && FitsGreek(filters)) || (hebrew && FitsHebrew(filters))) return null;

        var offending = filters.FirstOrDefault(f => !(greek && AppliesToGreek(f.Key)) && !(hebrew && AppliesToHebrew(f.Key))) ?? filters[0];
        var candidates = GrammarVocabulary.Keys.Where(k => k.Kind != GrammarKeyKind.Word && !k.GermanOnly && ((greek && AppliesToGreek(k)) || (hebrew && AppliesToHebrew(k)))).Select(k => k.Name).ToList();
        return new GrammarQueryError(GrammarErrorCodes.KeyNotInLanguage, offending.Text, candidates);
    }

    private async Task<GrammarSide?> ResolveSideAsync(IReadOnlyList<GrammarFilter> filters, IReadOnlyList<GrammarLemma> lemmas, bool hebrew, CancellationToken token)
    {
        var codes = filters.Count == 0 ? null : hebrew ? await _resolver.ResolveHebrewAsync(filters, token) : await _resolver.ResolveGreekAsync(filters, token);
        if (codes is { Count: 0 }) return null;

        return new GrammarSide(codes, lemmas.Count == 0 ? null : [.. lemmas.Where(l => l.IsHebrew == hebrew).SelectMany(l => StrongsKeys(l.Key))]);
    }

    /// <summary>The German side of a slot: its German lemma and <c>dt.</c> filters as the repository queries them; null when the slot constrains no German word.</summary>
    private static GrammarGermanSide? ToGermanSide(string? germanLemma, IReadOnlyList<GrammarFilter> germanFilters)
    {
        if (germanLemma is null && germanFilters.Count == 0) return null;

        return new GrammarGermanSide(germanLemma, [.. germanFilters.Select(f => new GrammarGermanFilter(
            f.Key.GermanColumn!,
            f.Key.Kind == GrammarKeyKind.Code ? [.. f.CodePatterns.Select(LikePattern)] : [.. f.Values.SelectMany(v => v.DbCodes ?? [])],
            f.Negated,
            f.Key.Kind == GrammarKeyKind.Code))]);
    }

    /// <summary>"VV*" → "VV%": <c>*</c> is the wildcard of the query language; LIKE's own wildcards are escaped.</summary>
    internal static string LikePattern(string pattern) =>
        pattern.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace('*', '%');

    /// <summary>
    /// Reads the word token of a slot: Strong's, script or transliteration as original lemmas; otherwise (roadmap 0029.7, step 4) a German lemma. <c>dt.Wort:</c> forces the German reading.
    /// Null lemma and German lemma with no error mean the slot has no word token.
    /// </summary>
    private async Task<(IReadOnlyList<GrammarLemma> Lemmas, string? GermanLemma, GrammarQueryError? Error)> ReadWordAsync(GrammarSlotSpec spec, CancellationToken token)
    {
        if (spec.Word is null) return ([], null, null);

        if (spec.Mode != GrammarWordMode.German)
        {
            var lemmas = await _words.ReadAsync(spec.Word, spec.Mode, token);
            if (lemmas.Count > 0) return (lemmas, null, null);
            if (spec.Mode != GrammarWordMode.Auto) return ([], null, new GrammarQueryError(GrammarErrorCodes.UnknownWord, spec.Word));
        }

        var german = spec.Word.ToLowerInvariant();
        return await _repository.GermanLemmaExistsAsync(german, token) ? ([], german, null) : ([], null, new GrammarQueryError(GrammarErrorCodes.UnknownWord, spec.Word));
    }

    /// <summary>Parses the query, reads the word tokens and resolves the filters of every slot (shared by the search and the chapter endpoints).</summary>
    private async Task<PreparedQuery> PrepareAsync(string? query, CancellationToken token)
    {
        var parsed = GrammarQueryParser.Parse(query);
        if (parsed.Error is not null) return new PreparedQuery([], parsed.Error);

        var slots = new List<ReadSlot>();
        foreach (var spec in parsed.Slots)
        {
            var (lemmas, germanLemma, wordError) = await ReadWordAsync(spec, token);
            if (wordError is not null) return new PreparedQuery([], wordError);

            var originalFilters = OriginalFilters(spec);
            var german = ToGermanSide(germanLemma, GermanFilters(spec));
            var (greek, hebrew) = SlotLanguages(lemmas);
            if (CheckKeysExist(originalFilters, lemmas.Count > 0, greek, hebrew) is { } keyError) return new PreparedQuery([], keyError);

            // A slot constrains the original word when it has an original lemma or original filters; otherwise it is a German-only slot.
            GrammarSlotQuery? slotQuery;
            if (lemmas.Count == 0 && originalFilters.Count == 0)
            {
                slotQuery = new GrammarSlotQuery(null, null, german);
            }
            else
            {
                var greekSide = greek && FitsGreek(originalFilters) ? await ResolveSideAsync(originalFilters, lemmas, false, token) : null;
                var hebrewSide = hebrew && FitsHebrew(originalFilters) ? await ResolveSideAsync(originalFilters, lemmas, true, token) : null;
                slotQuery = greekSide is null && hebrewSide is null ? null : new GrammarSlotQuery(greekSide, hebrewSide, german);
            }

            slots.Add(new ReadSlot(spec, lemmas, germanLemma, slotQuery));
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
        var wordKeys = hits.SelectMany(v => v).Select(m => m.WordKey).Distinct().ToList();
        var mappings = await ReadMappingsAsync(hits.SelectMany(v => v), token);

        response.Verses = [.. hits.OrderBy(v => v.Key).Select(v => new GrammarChapterVerse
        {
            Verse = v.Key % 1000,
            OriginalWordIds = [.. v.Where(m => !m.IsGerman).Select(m => m.WordId).Distinct().Order()],
            GermanPositions = [.. v.SelectMany(m => GermanPositionsOf(m, mappings)).Distinct().Order()],
        })];
        response.Total = wordKeys.Count;
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
        var (lemmas, germanLemma, wordError) = await ReadWordAsync(spec, token);
        if (wordError is not null) return new() { Error = wordError };

        var originalFilters = OriginalFilters(spec);
        var germanFilters = GermanFilters(spec);
        var (greek, hebrew) = SlotLanguages(lemmas);
        if (CheckKeysExist(originalFilters, lemmas.Count > 0, greek, hebrew) is { } keyError) return new() { Error = keyError };

        var germanKey = !string.IsNullOrWhiteSpace(key) && key.StartsWith(GrammarVocabulary.GermanPrefix, StringComparison.OrdinalIgnoreCase);
        var germanOnlySlot = lemmas.Count == 0 && originalFilters.Count == 0 && (germanLemma is not null || germanFilters.Count > 0);
        if (germanKey || (string.IsNullOrWhiteSpace(key) && germanOnlySlot)) return await GermanFacetsAsync(slot, germanKey ? key![GrammarVocabulary.GermanPrefix.Length..] : null, germanLemma, germanFilters, token);

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
            var rows = await _repository.CountCodesAsync(lemmas.Count == 0 ? null : [.. lemmas.Where(l => !l.IsHebrew).SelectMany(l => StrongsKeys(l.Key))], token);
            sides.Add((GrammarVocabulary.LanguageGreek, rows.ToDictionary(r => r.Code, r => r.Count), await _resolver.GreekCodesAsync(token)));
        }

        if (hebrew)
        {
            var rows = await _repository.CountHebrewCodesAsync(lemmas.Count == 0 ? null : [.. lemmas.Where(l => l.IsHebrew).SelectMany(l => StrongsKeys(l.Key))], token);
            sides.Add((GrammarVocabulary.LanguageHebrew, rows.ToDictionary(r => r.Code, r => r.Count), await _resolver.HebrewCodesAsync(token)));
        }

        var response = new SearchGrammarFacetsResponse { Slot = slot };
        foreach (var facetKey in facetKeys)
        {
            var others = originalFilters.Where(f => f.Key != facetKey).ToList();
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

    /// <summary>The facets of the German word (<c>dt.</c> keys): words of <c>Elb1871Morphology</c> per value, given the slot's German lemma and its other German filters. <paramref name="keyName"/> null = every key with a value list.</summary>
    private async Task<GrammarOutcome<SearchGrammarFacetsResponse>> GermanFacetsAsync(int slot, string? keyName, string? germanLemma, IReadOnlyList<GrammarFilter> germanFilters, CancellationToken token)
    {
        var allKeys = GrammarFacetFolder.GermanFacetKeys();
        var facetKeys = allKeys;
        if (keyName is not null)
        {
            var found = GrammarVocabulary.FindKey(keyName, true);
            if (found is null || !allKeys.Contains(found)) return new() { Error = new GrammarQueryError(GrammarErrorCodes.UnknownKey, GrammarVocabulary.GermanPrefix + keyName, [.. allKeys.Select(k => GrammarVocabulary.GermanPrefix + k.Name)]) };
            facetKeys = [found];
        }

        var response = new SearchGrammarFacetsResponse { Slot = slot };
        foreach (var facetKey in facetKeys)
        {
            var side = ToGermanSide(germanLemma, [.. germanFilters.Where(f => f.Key != facetKey)]);
            var rows = await _repository.CountGermanAsync(facetKey.GermanColumn!, side, token);
            var facet = GrammarFacetFolder.FoldGerman(facetKey, rows.ToDictionary(r => r.Code, r => r.Count));
            if (facet.Values.Count > 0) response.Facets.Add(facet);
        }

        return new() { Response = response };
    }

    private static int ChapterStart(int bibleBookId, int chapter) => bibleBookId * BookShift + chapter * ChapterSpan;

    private static bool HasAssignment(IEnumerable<GrammarMatchRow> verseMatches, int slotCount)
    {
        var rows = verseMatches.ToList();
        var perSlot = Enumerable.Range(0, slotCount).Select(i => rows.Where(m => m.Slot == i).Select(m => m.WordKey).ToHashSet()).ToList();
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
        // Rows of a slot that constrains the German word already carry their German position; German rows have no mapping.
        var rows = matches.Where(m => !m.IsGerman && m.GermanPosition is null).ToList();
        var greekIds = rows.Where(m => !m.IsHebrew).Select(m => m.WordId).Distinct().ToList();
        var greek = greekIds.Count == 0 ? [] : await _repository.ReadMappingsAsync(greekIds, token);
        var hebrewIds = rows.Where(m => m.IsHebrew).Select(m => m.WordId).Distinct().ToList();
        var hebrew = hebrewIds.Count == 0 ? [] : await _repository.ReadHebrewMappingsAsync(hebrewIds, token);
        return greek.Select(m => (Key: (false, m.StepWordId), m.PositionInVerse))
            .Concat(hebrew.Select(m => (Key: (true, m.StepWordId), m.PositionInVerse)))
            .ToLookup(x => x.Key, x => x.PositionInVerse);
    }

    /// <summary>The German positions of one candidate: the constrained German word when the slot has one, otherwise the words mapped to the original word.</summary>
    private static IEnumerable<int> GermanPositionsOf(GrammarMatchRow row, ILookup<(bool IsHebrew, int WordId), int> mappings) =>
        row.GermanPosition is { } position ? [position] : mappings[(row.IsHebrew, row.WordId)];

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
            Matches = [.. matchesByVerse[id].GroupBy(m => (m.Slot, m.WordKey)).Select(g => ToMatch([.. g], mappings, hebrewLemmas)).OrderBy(m => m.Slot)],
        })];
    }

    /// <summary>One match: an original word (or a German word of a German-only slot) with the German positions of all its rows; the rows are in SQL order, so the matches keep the order of the original words.</summary>
    private static GrammarMatch ToMatch(List<GrammarMatchRow> rows, ILookup<(bool IsHebrew, int WordId), int> mappings, IReadOnlyDictionary<string, string> hebrewLemmas) => new()
    {
        Slot = rows[0].Slot,
        GermanPositions = [.. rows.SelectMany(m => GermanPositionsOf(m, mappings)).Distinct().Order()],
        Original = rows[0].IsGerman ? null : ToOriginal(rows[0], hebrewLemmas),
    };

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
        ReadAs = slot.GermanLemma is not null ? "german" : slot.Spec.Word is null ? "any" : "lemma",
        GermanLemma = slot.GermanLemma,
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
