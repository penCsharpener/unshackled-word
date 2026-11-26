using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>Folds per-code word counts into the value counts of search keys (roadmap 0029.5); pure, the codes come from <see cref="IMorphologyCodeIndex"/>.</summary>
public static class GrammarFacetFolder
{
    /// <summary>Keys that have a fixed value list of the given language: what the facets of "all keys" offer.</summary>
    public static IReadOnlyList<GrammarKeyDef> FacetKeys(GrammarLanguages language) =>
        [.. GrammarVocabulary.Keys.Where(k => k is { Kind: GrammarKeyKind.Morphology, Selector: not null } && k.Languages.HasFlag(language))];

    /// <summary>
    /// The facet of <paramref name="key"/>: for every value the sum of the counts of the codes that carry it, in vocabulary order, without zero values.
    /// <paramref name="counts"/> holds the words per code (codes missing from <paramref name="codes"/> are ignored); the caller has already removed the slot's own filter on the key.
    /// </summary>
    public static GrammarFacet Fold(GrammarKeyDef key, IReadOnlyDictionary<string, int> counts, IEnumerable<MorphologyCodeEntry> codes)
    {
        var perValue = new Dictionary<string, int>();
        foreach (var entry in codes)
        {
            if (!counts.TryGetValue(entry.Code, out var count) || count <= 0) continue;
            if (key.Selector!(entry.Keys) is not { } id) continue;
            perValue[id] = perValue.GetValueOrDefault(id) + count;
        }

        return new GrammarFacet
        {
            Key = key.Name,
            Values = [.. key.Values.Where(v => perValue.ContainsKey(v.Id)).Select(v => new GrammarFacetValue { Key = v.Id, LabelDe = v.De, Count = perValue[v.Id] })],
        };
    }
}
