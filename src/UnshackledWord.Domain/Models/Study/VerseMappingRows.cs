namespace UnshackledWord.Domain.Models.Study;

public sealed class VerseGermanWordRow
{
    public int Id { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>STTS tag of the word (Elb1871Morphology.Stts); null when the word has no morphology row. Needed for the APPRART contraction rule.</summary>
    public string? Stts { get; set; }
}

public sealed class VerseOriginalWordRow
{
    public int Id { get; set; }
    public int Position { get; set; }
    public string Original { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? Lemma { get; set; }
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string? MorphologyCode { get; set; }

    /// <summary>Hebrew only: StepHebrewWords.ExpandedStrongTags (else DisambiguatedStrongs), the morphemes of the word; null for Greek.</summary>
    public string? MorphemeTags { get; set; }
}

/// <summary>One row of Elb1871GreekMapping / Elb1871HebrewMapping, reduced to what the mapping editor edits.</summary>
public sealed class VerseMappingRow
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }

    /// <summary>Compound part of the German word (<c>ernte</c> of <c>Gerstenernte</c>); null for a whole-word link. Carried into revisions.</summary>
    public string? GermanWordPart { get; set; }

    /// <summary>1-based order of <see cref="GermanWordPart"/> inside the word; null for a whole-word link or a part stored before the order existed.</summary>
    public int? PartOrder { get; set; }

    /// <summary>head or unit (roadmap 0018); stored rows always have one, added words keep head.</summary>
    public string Role { get; set; } = "head";

    /// <summary>Hebrew only: the morpheme of the original word the row renders; null = the main morpheme. Never written for Greek (derived from STEP).</summary>
    public string? StrongsNumber { get; set; }

    /// <summary>Lemma of the compound part; only together with <see cref="GermanWordPart"/>.</summary>
    public string? PartLemma { get; set; }

    /// <summary>True on the head rows of a morpheme whose several heads the maintainer accepted.</summary>
    public bool MultiHead { get; set; }

    /// <summary>Written on save (the user who saved the row); not read back.</summary>
    public int? CreatedByUserId { get; set; }
}
