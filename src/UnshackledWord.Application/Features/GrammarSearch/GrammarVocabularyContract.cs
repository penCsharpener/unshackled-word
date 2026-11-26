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
            Languages = languages,
            Kind = key.Kind == GrammarKeyKind.Word ? "word" : "filter",
            FreeValue = key.Kind is GrammarKeyKind.Code or GrammarKeyKind.Word,
            Values = [.. key.Values.Select(v => new GrammarVocabularyValue
            {
                Key = v.Id,
                LabelDe = v.De,
                LabelEn = v.En,
                Aliases = [.. GrammarVocabulary.ListedAliases(v)],
                Languages = languages,
            })],
        };
    }

    private static List<string> ToLanguages(GrammarLanguages languages)
    {
        List<string> result = [];
        if (languages.HasFlag(GrammarLanguages.Greek)) result.Add(GrammarVocabulary.LanguageGreek);
        if (languages.HasFlag(GrammarLanguages.Hebrew)) result.Add(GrammarVocabulary.LanguageHebrew);
        if (languages.HasFlag(GrammarLanguages.Aramaic)) result.Add(GrammarVocabulary.LanguageAramaic);
        return result;
    }
}
