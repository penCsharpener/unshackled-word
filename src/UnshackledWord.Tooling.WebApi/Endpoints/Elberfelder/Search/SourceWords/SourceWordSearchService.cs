using System.Globalization;
using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchSourceWords;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

public interface ISourceWordSearchService
{
    /// <summary>Null while the data quality facts were never computed (the endpoint answers 503).</summary>
    Task<SearchSourceWordsResponse?> SearchAsync(string q, int limit, CancellationToken ct);
}

/// <summary>
/// Public lookup of Hebrew / Greek words: per search word the lemma entries (<see cref="StrongsLexiconIndex"/>: Strong's number, script without diacritics,
/// transliteration, gloss) plus the inflected forms of the text that match (<see cref="SourceFormIndex"/>), grouped under their lemma.
/// </summary>
public sealed partial class SourceWordSearchService : ISourceWordSearchService
{
    private const int LexiconPool = 200;
    private const int FormPool = 2000;
    private const int FormsPerLemma = 6;
    private const int MaxRefForms = 30;
    private const int RefsPerForm = 3;

    private readonly StrongsLexiconIndex _lexicon;
    private readonly SourceFormIndex _forms;
    private readonly ISourceWordRefs _refs;

    public SourceWordSearchService(StrongsLexiconIndex lexicon, SourceFormIndex forms, ISourceWordRefs refs)
    {
        _lexicon = lexicon;
        _forms = forms;
        _refs = refs;
    }

    [GeneratedRegex(@"[^\p{L}\p{M}\p{N}'’ʼ]")]
    private static partial Regex NonWordRegex();

    /// <summary>Whitespace separated words without punctuation (Greek question mark and ano teleia, maqaf, commas), distinct by folded spelling, query order kept.</summary>
    public static IReadOnlyList<string> Tokenize(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => NonWordRegex().Replace(w, string.Empty))
            .Where(w => w.Length > 0 && seen.Add(TranslitFolding.FoldScript(w)))];
    }

    /// <summary>Strong's number as the Strong's page route writes it: "H0430" → "H430".</summary>
    public static string DisplayKey(string key) => key[..1] + int.Parse(key.AsSpan(1), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private sealed class Candidate
    {
        public required LexiconEntry Entry { get; init; }
        public required string MatchedBy { get; set; }
        public required int Tier { get; set; }
        public List<SourceFormEntry> Forms { get; } = [];
    }

    public async Task<SearchSourceWordsResponse?> SearchAsync(string q, int limit, CancellationToken ct)
    {
        if (!await _forms.IsReadyAsync(ct)) return null;

        var response = new SearchSourceWordsResponse();
        foreach (var token in Tokenize(q).Take(Domain.WebApi.Elberfelder.SearchSourceWords.SearchSourceWordsRequest.MaxTokens))
        {
            response.Tokens.Add(new SearchSourceWordsToken { Token = token, Lemmas = await LookupAsync(token, limit, ct) });
        }

        await AddRefsAsync(response, ct);
        return response;
    }

    private async Task<List<SourceWordLemma>> LookupAsync(string token, int limit, CancellationToken ct)
    {
        var folded = TranslitFolding.FoldScript(token);
        var candidates = new Dictionary<string, Candidate>(StringComparer.Ordinal);

        foreach (var hit in await _lexicon.SearchAsync(token, LexiconPool, ct))
        {
            var exact = hit.MatchedBy == "script" && TranslitFolding.FoldScript(hit.Entry.Lemma) == folded;
            candidates[hit.Entry.Key] = new Candidate { Entry = hit.Entry, MatchedBy = hit.MatchedBy, Tier = TierOf(hit.MatchedBy, exact) };
        }

        // Inflected forms only for script input: Latin words would match the transliteration of nothing but noise.
        if (folded.Length >= 2 && !TranslitFolding.IsLatinInput(folded) && TranslitFolding.ParseStrongs(token).Count == 0)
        {
            var matches = await _forms.MatchAsync(folded, FormPool, ct);
            var entries = await _lexicon.GetManyAsync(matches.Select(m => m.StrongsKey).Distinct(), ct);
            foreach (var match in matches)
            {
                if (!entries.TryGetValue(match.StrongsKey, out var entry)) continue;
                var tier = match.Folded == folded ? 1 : 2;
                if (!candidates.TryGetValue(match.StrongsKey, out var candidate))
                {
                    candidate = candidates[match.StrongsKey] = new Candidate { Entry = entry, MatchedBy = "form", Tier = tier };
                }

                candidate.Tier = Math.Min(candidate.Tier, tier);
                candidate.Forms.Add(match);
            }
        }

        var ranked = new List<(Candidate Candidate, int Occurrences)>();
        foreach (var candidate in candidates.Values)
        {
            ranked.Add((candidate, await _forms.OccurrencesOfAsync(candidate.Entry.Key, ct)));
        }

        var lemmas = new List<SourceWordLemma>();
        foreach (var (candidate, occurrences) in ranked
                     .OrderBy(r => r.Candidate.Tier)
                     .ThenByDescending(r => r.Occurrences)
                     .ThenBy(r => r.Candidate.Entry.Key, StringComparer.Ordinal)
                     .Take(limit))
        {
            // Matching forms (the exact one first); a lemma found by number, lemma or gloss shows its most frequent forms instead.
            var shown = candidate.Forms.Count > 0
                ? candidate.Forms.OrderByDescending(f => f.Folded == folded).ThenByDescending(f => f.Count).ToList()
                : [.. await _forms.FormsOfAsync(candidate.Entry.Key, ct)];
            lemmas.Add(new SourceWordLemma
            {
                Strongs = DisplayKey(candidate.Entry.Key),
                Language = candidate.Entry.Lang == "G" ? "greek" : "hebrew",
                Lemma = candidate.Entry.Lemma,
                Transliteration = candidate.Entry.Transliteration,
                Gloss = candidate.Entry.Gloss,
                Occurrences = occurrences,
                MatchedBy = candidate.MatchedBy,
                Forms = [.. shown.Take(FormsPerLemma).Select(f => new SourceWordForm { Form = f.Form, Occurrences = f.Count })],
            });
        }

        return lemmas;
    }

    /// <summary>Ranking tier: Strong's number, then the exact lemma or form, then prefix matches, transliteration, consonant skeleton, gloss.</summary>
    private static int TierOf(string matchedBy, bool exact) => matchedBy switch
    {
        "strongs" => 0,
        "script" => exact ? 1 : 2,
        "translit" => 3,
        "skeleton" => 4,
        _ => 5,
    };

    /// <summary>Up to three verses per shown form (first ones in canonical order), for the first <see cref="MaxRefForms"/> forms of the result.</summary>
    private async Task AddRefsAsync(SearchSourceWordsResponse response, CancellationToken ct)
    {
        var wanted = response.Tokens
            .SelectMany(t => t.Lemmas)
            .SelectMany(l => l.Forms.Select(f => (Lemma: l, Form: f)))
            .Take(MaxRefForms)
            .ToList();
        if (wanted.Count == 0) return;

        var rows = await _refs.ReadAsync([.. wanted.Select(w => (ToKey(w.Lemma.Strongs), w.Form.Form))], RefsPerForm, ct);

        var byForm = rows.ToLookup(r => (r.Strongs, r.Form));
        foreach (var (lemma, form) in wanted)
        {
            form.Refs = [.. byForm[(ToKey(lemma.Strongs), form.Form)]
                .OrderBy(r => r.BibleBookId).ThenBy(r => r.Chapter).ThenBy(r => r.Verse)
                .Select(r => new SourceWordRef { BibleBookId = r.BibleBookId, Chapter = r.Chapter, Verse = r.Verse })];
        }
    }

    /// <summary>"H430" → "H0430" (the key of the lexicon and of DqWordFacts).</summary>
    private static string ToKey(string display) => display[..1] + int.Parse(display.AsSpan(1), CultureInfo.InvariantCulture).ToString("D4", CultureInfo.InvariantCulture);
}
