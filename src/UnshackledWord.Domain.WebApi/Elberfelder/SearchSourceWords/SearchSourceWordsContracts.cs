namespace UnshackledWord.Domain.WebApi.Elberfelder.SearchSourceWords;

public sealed class SearchSourceWordsRequest
{
    public const int MaxQueryLength = 200;
    public const int MaxTokens = 5;
    public const int MaxTokenLength = 50;
    public const int DefaultLimit = 8;
    public const int MaxLimit = 30;

    /// <summary>
    /// Whitespace separated search words: Hebrew / Greek script (diacritics and final forms optional), a Strong's number (G1577, h430),
    /// a transliteration or an English gloss. Every word is looked up on its own.
    /// </summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>Lemma entries per word.</summary>
    public int Limit { get; set; } = DefaultLimit;
}

public sealed class SearchSourceWordsResponse
{
    /// <summary>One entry per distinct search word, in query order.</summary>
    public List<SearchSourceWordsToken> Tokens { get; set; } = [];
}

public sealed class SearchSourceWordsToken
{
    /// <summary>The search word as looked up (punctuation removed).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Best matches first: Strong's number, exact lemma or form, then prefix matches by occurrences.</summary>
    public List<SourceWordLemma> Lemmas { get; set; } = [];
}

public sealed class SourceWordLemma
{
    /// <summary>"G1577", "H430" (no leading zeros, as the Strong's page route expects).</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>"greek" or "hebrew".</summary>
    public string Language { get; set; } = string.Empty;

    public string Lemma { get; set; } = string.Empty;

    public string Transliteration { get; set; } = string.Empty;

    public string Gloss { get; set; } = string.Empty;

    /// <summary>Occurrences of this Strong's number in the source text (0 when the stats do not know it).</summary>
    public int Occurrences { get; set; }

    /// <summary>"strongs", "script", "translit", "skeleton", "gloss" (lemma entry) or "form" (found through an inflected form).</summary>
    public string MatchedBy { get; set; } = string.Empty;

    /// <summary>Inflected forms of the lemma found in the text that match the word (else its most frequent forms), most frequent first.</summary>
    public List<SourceWordForm> Forms { get; set; } = [];
}

public sealed class SourceWordForm
{
    /// <summary>The form as printed, with diacritics.</summary>
    public string Form { get; set; } = string.Empty;

    public int Occurrences { get; set; }

    /// <summary>A few verses (canonical order) that contain the form; empty beyond the first forms of the result.</summary>
    public List<SourceWordRef> Refs { get; set; } = [];
}

public sealed class SourceWordRef
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }

    public int Verse { get; set; }
}
