namespace UnshackledWord.Domain.Models.Study;

public sealed class VerseGermanWordRow
{
    public int Id { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class VerseOriginalWordRow
{
    public int Id { get; set; }
    public int Position { get; set; }
    public string Original { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string? MorphologyCode { get; set; }
}

/// <summary>One row of Elb1871GreekMapping / Elb1871HebrewMapping, reduced to what the mapping editor edits.</summary>
public sealed class VerseMappingRow
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }
}
