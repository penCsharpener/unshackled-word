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

// ---- Snapshot (Stats* materialized views, roadmap 0008) ----

/// <summary>One match key of a Strong's number in the snapshot; BookCounts is the jsonb text <c>[{"b":1,"c":5,"v":4}]</c>.</summary>
public sealed class SnapshotStrongsRow
{
    public string Strongs { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public int MappedCount { get; set; }
    public string BookCounts { get; set; } = "[]";
}

/// <summary>A German lemma + PoS rendering a Strong's number; Count is summed over the match keys, Key is the lower-cased lemma (or form), Word its display spelling.</summary>
public sealed class SnapshotRenderingRow
{
    public string Word { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public int Count { get; set; }
}

/// <summary>A German lemma + PoS in the snapshot. LemmaOccurrences sums all PoS rows of the same lemma key (frequency rule).</summary>
public sealed class SnapshotLemmaRow
{
    public string Key { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public string Word { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public int LemmaOccurrences { get; set; }
    public int UnmappedCount { get; set; }
    public string BookCounts { get; set; } = "[]";
}

/// <summary>One original behind a German lemma + PoS (top-N lookup of the smart search); Count of TotalCount, lexicon columns LEFT JOINed.</summary>
public sealed class SnapshotLemmaOriginalRow
{
    public string Key { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public string Strongs { get; set; } = string.Empty;
    public int Count { get; set; }
    public int LemmaTotal { get; set; }
    public int TotalCount { get; set; }
    public int? LexiconLanguageId { get; set; }
    public string? Lemma { get; set; }
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }
}

/// <summary>One entry of the BookCounts jsonb: book, count, verse count and (German lemma view only) unmapped count.</summary>
public sealed class SnapshotBookCount
{
    public int B { get; set; }
    public int C { get; set; }
    public int V { get; set; }
    public int U { get; set; }
}

/// <summary>A German lemma key (lower-cased) with its PoS (null when the word has no morphology row).</summary>
public sealed record SnapshotLemmaKey(string Key, string? Pos);
