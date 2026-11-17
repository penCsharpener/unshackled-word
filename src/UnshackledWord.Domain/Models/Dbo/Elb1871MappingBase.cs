namespace UnshackledWord.Domain.Models.Dbo;

public class Elb1871MappingBase
{
    public int Id { get; set; }
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentGermanWordId { get; set; }
    public string? GermanWordPart { get; set; }

    /// <summary>head or unit (roadmap 0018); the column defaults to head.</summary>
    public string Role { get; set; } = "head";

    /// <summary>Lemma of the compound part <see cref="GermanWordPart"/>; null without a part.</summary>
    public string? PartLemma { get; set; }

    /// <summary>The maintainer accepted several heads on this source morpheme.</summary>
    public bool MultiHead { get; set; }
}
