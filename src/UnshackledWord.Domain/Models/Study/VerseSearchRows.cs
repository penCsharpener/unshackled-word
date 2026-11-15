namespace UnshackledWord.Domain.Models.Study;

/// <summary>Stemmed lexeme of one query word (ts_lexize on the German dictionary without stop words).</summary>
public sealed class VerseSearchLexemeRow
{
    public string Word { get; set; } = string.Empty;
    public string? Lexeme { get; set; }
}

public sealed class VerseSearchBookCountRow
{
    public int BookId { get; set; }
    public int Count { get; set; }
}

public sealed class VerseSearchHitRow
{
    public int HebRefId { get; set; }

    /// <summary>Set when the revision variant is the text to show (it matched the query).</summary>
    public int? RevisionVerseId { get; set; }

    public int? Level { get; set; }
}

/// <summary>One word of a hit verse: its text, the stem lexemes of the plain word and its morphology lemma (lower-cased, null without morphology).</summary>
public sealed class VerseSearchWordRow
{
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string WordInContext { get; set; } = string.Empty;
    public string[] Stems { get; set; } = [];
    public string? Lemma { get; set; }
}
