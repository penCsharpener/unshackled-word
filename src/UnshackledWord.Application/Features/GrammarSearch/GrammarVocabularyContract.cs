using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>Maps <see cref="GrammarVocabulary"/> to the public contract; the single source stays the vocabulary.</summary>
public static class GrammarVocabularyContract
{
    public static GrammarVocabularyResponse Build() => new()
    {
        GermanPrefix = GrammarVocabulary.GermanPrefix,
        GermanKeysEnabled = GrammarVocabulary.GermanKeysEnabled,
        Keys = [.. GrammarVocabulary.Keys.Select(ToKey)],
    };

    private static GrammarVocabularyKey ToKey(GrammarKeyDef key)
    {
        var languages = ToLanguages(key.Languages);
        return new GrammarVocabularyKey
        {
            Name = key.Name,
            Aliases = [.. key.Aliases],
            GermanPrefixAllowed = key.GermanPrefix && GrammarVocabulary.GermanKeysEnabled,
            GermanOnly = key.GermanOnly,
            Languages = languages,
            Kind = key.Kind == GrammarKeyKind.Word ? "word" : "filter",
            FreeValue = key.Kind is GrammarKeyKind.Code or GrammarKeyKind.Word,
            Values = [.. key.Values.Select(v => ToValue(v, languages))],
            GermanValues = [.. key.GermanValues.Select(v => ToValue(v, []))],
        };
    }

    private static GrammarVocabularyValue ToValue(GrammarValueDef value, List<string> languages) => new()
    {
        Key = value.Id,
        LabelDe = value.De,
        LabelEn = value.En,
        Aliases = [.. GrammarVocabulary.ListedAliases(value)],
        Languages = languages,
    };

    private static List<string> ToLanguages(GrammarLanguages languages)
    {
        List<string> result = [];
        if (languages.HasFlag(GrammarLanguages.Greek)) result.Add(GrammarVocabulary.LanguageGreek);
        if (languages.HasFlag(GrammarLanguages.Hebrew)) result.Add(GrammarVocabulary.LanguageHebrew);
        if (languages.HasFlag(GrammarLanguages.Aramaic)) result.Add(GrammarVocabulary.LanguageAramaic);
        return result;
    }
}
