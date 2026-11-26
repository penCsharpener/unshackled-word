namespace UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

/// <summary><c>GET api/elberfelder/search/grammar/vocabulary</c> (roadmap 0029.3): the keys and values the grammar query accepts, for autocomplete and the cheat sheet. Static per deployment.</summary>
public sealed class GrammarVocabularyResponse
{
    /// <summary>Prefix that applies a key to the German word, e.g. <c>dt.</c>.</summary>
    public string GermanPrefix { get; set; } = string.Empty;

    /// <summary>False while the parser rejects <c>dt.</c> keys; then <c>germanPrefixAllowed</c> is false on every key (true since roadmap 0029.7).</summary>
    public bool GermanKeysEnabled { get; set; }

    public List<GrammarVocabularyKey> Keys { get; set; } = [];
}

public sealed class GrammarVocabularyKey
{
    /// <summary>Canonical name, e.g. <c>Kasus</c>.</summary>
    public string Name { get; set; } = string.Empty;

    public List<string> Aliases { get; set; } = [];

    /// <summary>Whether <c>dt.</c> may precede the key (and the parser accepts it today).</summary>
    public bool GermanPrefixAllowed { get; set; }

    /// <summary>True for keys that exist only after <c>dt.</c> (<c>Verbform</c>, <c>STTS</c>, <c>Wort</c>): a client must not offer them without the prefix. Their <c>languages</c> are empty.</summary>
    public bool GermanOnly { get; set; }

    /// <summary><c>greek</c>, <c>hebrew</c>, <c>aramaic</c>: the original languages the key applies to.</summary>
    public List<string> Languages { get; set; } = [];

    /// <summary><c>filter</c> (limits a word) or <c>word</c> (<c>Strong</c>/<c>Lemma</c>, forces how a word token is read).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>True when the value is free text (<c>Code</c> pattern, Strong's number, lemma): <c>values</c> is empty and the UI offers no value list.</summary>
    public bool FreeValue { get; set; }

    /// <summary>The values of the original word (after the key without prefix).</summary>
    public List<GrammarVocabularyValue> Values { get; set; } = [];

    /// <summary>The values after <c>dt.</c> (the German tag set differs: Präteritum, finit, Positiv ...); empty when the key has no German filter or a free value (<c>STTS</c>).</summary>
    public List<GrammarVocabularyValue> GermanValues { get; set; } = [];
}

public sealed class GrammarVocabularyValue
{
    /// <summary>Canonical key (the <c>grammar.*</c> key of the frontend translations, or a language id).</summary>
    public string Key { get; set; } = string.Empty;

    public string LabelDe { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public List<string> Aliases { get; set; } = [];

    public List<string> Languages { get; set; } = [];
}
