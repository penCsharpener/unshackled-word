namespace UnshackledWord.Domain.Models.Dbo;

public sealed class Elb1871HebrewMappingDbo : Elb1871MappingBase
{
    public const string DboName = "\"unshackled-word\".\"Elb1871HebrewMapping\"";

    /// <summary>Morpheme of the Hebrew word this row renders (H9001 of וַיֹּאמֶר); null = the main morpheme (roadmap 0018).</summary>
    public string? StrongsNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
