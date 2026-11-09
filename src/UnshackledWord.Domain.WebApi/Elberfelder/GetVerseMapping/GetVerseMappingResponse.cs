namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

public sealed class GetVerseMappingResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>"greek" (New Testament) or "hebrew" (Old Testament).</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>False while the write gate (MappingEditing:Enabled) is closed; PUT then answers 403.</summary>
    public bool EditingEnabled { get; set; }

    /// <summary>Elberfelder words of the verse in reading order.</summary>
    public List<VerseMappingGermanWord> GermanWords { get; set; } = [];

    /// <summary>STEP Greek/Hebrew words of the verse in reading order (read live from the STEP tables).</summary>
    public List<VerseMappingOriginalWord> OriginalWords { get; set; } = [];

    public List<VerseMappingLink> Links { get; set; } = [];
}

public sealed class VerseMappingGermanWord
{
    public int Id { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class VerseMappingOriginalWord
{
    public int Id { get; set; }

    /// <summary>StepGreekWords/StepHebrewWords.PositionInVerse.</summary>
    public int Position { get; set; }

    public string Original { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string? MorphologyCode { get; set; }
}

/// <summary>
/// One mapping row. A normal link has <see cref="StepWordId"/> and no parent. An added word
/// (<see cref="IsAddedWord"/>) has a <see cref="ParentElbWordId"/> and no original word.
/// </summary>
public sealed class VerseMappingLink
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }
}
