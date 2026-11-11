namespace UnshackledWord.Domain.Models.Study;

/// <summary>Occurrences of a Strong's number in one book (Elberfelder book numbering). AramaicCount counts Hebrew-table words with Aramaic grammar.</summary>
public sealed class StrongsBookCountRow
{
    public int BibleBookId { get; set; }
    public int Count { get; set; }
    public int VerseCount { get; set; }
    public int AramaicCount { get; set; }
}

/// <summary>Occurrences of one match key (lexicon variant).</summary>
public sealed class StrongsKeyCountRow
{
    public string Strongs { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>One German rendering of a Strong's number: Key is the lower-cased German word key, Word its most frequent spelling.</summary>
public sealed class StrongsRenderingRow
{
    public string Word { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>A German word in one book: word count, verse count, words without a non-added mapping, and the most frequent spelling.</summary>
public sealed class WordBookCountRow
{
    public int BibleBookId { get; set; }
    public int Count { get; set; }
    public int VerseCount { get; set; }
    public int UnmappedCount { get; set; }
    public string Word { get; set; } = string.Empty;
}

/// <summary>A Strong's number behind a German word, with the lexicon entry (LEFT JOIN, so possibly empty).</summary>
public sealed class WordOriginalRow
{
    public string Strongs { get; set; } = string.Empty;
    public int Count { get; set; }
    public int TotalCount { get; set; }
    public int? LexiconLanguageId { get; set; }
    public string? Lemma { get; set; }
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }
}

/// <summary>Optional concordance filters; both are combined with AND. Rendering is a German word key (compared lower-cased).</summary>
public sealed record StrongsOccurrenceFilter(int? Book = null, string? Rendering = null)
{
    public static StrongsOccurrenceFilter None { get; } = new();
}
