using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar;

/// <summary>
/// Reads a word token of a grammar query as Greek or Hebrew lemmas, with the lexicon of the source word search (roadmap 0029.1, 0029.6). First match wins: Strong's number, then the exact
/// Greek or Hebrew lemma or printed form (diacritics and vowels ignored), then the exact transliteration (<c>agape</c> is G26, <c>bara</c> is H1254). No gloss, skeleton or prefix tiers: a grammar query wants the word it names.
/// <c>Strong:</c> forces the first, <c>Lemma:</c> the last two steps.
/// </summary>
public sealed class GrammarWordReader : IGrammarWordReader
{
    private const int TranslitPool = 2000;
    private const int FormPool = 2000;

    private readonly StrongsLexiconIndex _lexicon;
    private readonly SourceFormIndex _forms;

    public GrammarWordReader(StrongsLexiconIndex lexicon, SourceFormIndex forms)
    {
        _lexicon = lexicon;
        _forms = forms;
    }

    public async Task<IReadOnlyList<GrammarLemma>> ReadAsync(string token, GrammarWordMode mode, CancellationToken ct = default)
    {
        if (mode != GrammarWordMode.Lemma)
        {
            var strongs = TranslitFolding.ParseStrongs(token).ToList();
            var entries = (await _lexicon.GetManyAsync(strongs, ct)).Values.ToList();
            if (entries.Count > 0 || mode == GrammarWordMode.Strongs) return [.. entries.Select(ToLemma)];
        }

        var folded = TranslitFolding.FoldScript(token);
        if (folded.Length == 0) return [];

        return TranslitFolding.IsLatinInput(folded) ? await ByTransliterationAsync(token, ct) : await ByScriptAsync(token, folded, ct);
    }

    private async Task<IReadOnlyList<GrammarLemma>> ByTransliterationAsync(string token, CancellationToken ct)
    {
        var latin = TranslitFolding.Latin(token);
        var hits = await _lexicon.SearchAsync(token, TranslitPool, ct);
        return [.. hits
            .Where(h => h.MatchedBy == "translit" && h.Entry.FoldedTranslit.Split(' ').Contains(latin, StringComparer.Ordinal))
            .Select(h => ToLemma(h.Entry))];
    }

    private async Task<IReadOnlyList<GrammarLemma>> ByScriptAsync(string token, string folded, CancellationToken ct)
    {
        var found = new Dictionary<string, LexiconEntry>(StringComparer.Ordinal);
        foreach (var hit in await _lexicon.SearchAsync(token, TranslitPool, ct))
        {
            if (hit.MatchedBy == "script" && TranslitFolding.FoldScript(hit.Entry.Lemma) == folded) found[hit.Entry.Key] = hit.Entry;
        }

        if (await _forms.IsReadyAsync(ct))
        {
            var forms = (await _forms.MatchAsync(folded, FormPool, ct)).Where(f => f.Folded == folded).ToList();
            var entries = await _lexicon.GetManyAsync(forms.Select(f => f.StrongsKey).Distinct(), ct);
            foreach (var entry in entries.Values) found.TryAdd(entry.Key, entry);
        }

        return [.. found.Values.OrderBy(e => e.Key, StringComparer.Ordinal).Select(ToLemma)];
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadHebrewLemmasAsync(IEnumerable<string> baseKeys, CancellationToken ct = default) =>
        (await _lexicon.GetManyAsync(baseKeys, ct)).ToDictionary(e => e.Key, e => e.Value.Lemma, StringComparer.Ordinal);

    private static GrammarLemma ToLemma(LexiconEntry entry) => new(entry.Key, entry.Lemma, entry.Transliteration, entry.Gloss);
}
