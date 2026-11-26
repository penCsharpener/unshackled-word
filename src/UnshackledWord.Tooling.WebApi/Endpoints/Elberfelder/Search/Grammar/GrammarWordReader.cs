using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar;

/// <summary>
/// Reads a word token of a grammar query as Greek lemmas, with the lexicon of the source word search (roadmap 0029.1). First match wins: Strong's number, then the exact
/// Greek lemma or printed form (diacritics ignored), then the exact transliteration. No gloss, skeleton or prefix tiers: a grammar query wants the word it names.
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

    public async Task<IReadOnlyList<GrammarLemma>> ReadGreekAsync(string token, GrammarWordMode mode, CancellationToken ct = default)
    {
        if (mode != GrammarWordMode.Lemma)
        {
            var strongs = TranslitFolding.ParseStrongs(token).Where(k => k[0] == 'G').ToList();
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
            .Where(h => h.MatchedBy == "translit" && h.Entry.Lang == "G" && h.Entry.FoldedTranslit.Split(' ').Contains(latin, StringComparer.Ordinal))
            .Select(h => ToLemma(h.Entry))];
    }

    private async Task<IReadOnlyList<GrammarLemma>> ByScriptAsync(string token, string folded, CancellationToken ct)
    {
        var found = new Dictionary<string, LexiconEntry>(StringComparer.Ordinal);
        foreach (var hit in await _lexicon.SearchAsync(token, TranslitPool, ct))
        {
            if (hit.MatchedBy == "script" && hit.Entry.Lang == "G" && TranslitFolding.FoldScript(hit.Entry.Lemma) == folded) found[hit.Entry.Key] = hit.Entry;
        }

        if (await _forms.IsReadyAsync(ct))
        {
            var forms = (await _forms.MatchAsync(folded, FormPool, ct)).Where(f => f.Folded == folded && f.StrongsKey[0] == 'G').ToList();
            var entries = await _lexicon.GetManyAsync(forms.Select(f => f.StrongsKey).Distinct(), ct);
            foreach (var entry in entries.Values) found.TryAdd(entry.Key, entry);
        }

        return [.. found.Values.OrderBy(e => e.Key, StringComparer.Ordinal).Select(ToLemma)];
    }

    private static GrammarLemma ToLemma(LexiconEntry entry) => new(entry.Key, entry.Lemma, entry.Transliteration, entry.Gloss);
}
