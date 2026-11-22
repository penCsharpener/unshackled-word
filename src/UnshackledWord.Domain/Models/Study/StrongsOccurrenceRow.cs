namespace UnshackledWord.Domain.Models.Study;

/// <summary>One original-language word with a Strong's number, located in the Elberfelder verse numbering, with the words of that verse.</summary>
public sealed class StrongsOccurrenceRow
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string? Original { get; set; }

    /// <summary>Position of <see cref="Original"/> among the original words of its verse.</summary>
    public int OriginalPosition { get; set; }

    /// <summary>Positions of all original words of the verse, in verse order; null when none are stored. Same length as <see cref="OriginalWords"/>.</summary>
    public int[]? OriginalPositions { get; set; }

    /// <summary>The original words of the verse as written, in verse order.</summary>
    public string[]? OriginalWords { get; set; }

    /// <summary>Positions in the verse of the Elberfelder words mapped to this original word (added words excluded); null when unmapped.</summary>
    public int[]? MatchedPositions { get; set; }

    /// <summary>Positions in the verse of all Elberfelder words, in verse order; null when the verse is missing. Same length as <see cref="VerseWords"/>.</summary>
    public int[]? VersePositions { get; set; }

    /// <summary>The Elberfelder words of the verse as written (with punctuation), in verse order; null when the verse is missing.</summary>
    public string[]? VerseWords { get; set; }
}
