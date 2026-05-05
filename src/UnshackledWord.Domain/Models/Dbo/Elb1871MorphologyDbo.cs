namespace UnshackledWord.Domain.Models.Dbo;

public sealed class Elb1871MorphologyDbo
{
    public const string DboName = "\"unshackled-word\".\"Elb1871Morphology\"";

    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string Lemma { get; set; } = default!;
    public string PartOfSpeech { get; set; } = default!;
    public string Stts { get; set; } = default!;
    public string? Degree { get; set; }
    public string? VerbForm { get; set; }
    public string? Tense { get; set; }
    public string? Person { get; set; }
    public string? Number { get; set; }
    public string? Mood { get; set; }
    public string? Case { get; set; }
    public string? Gender { get; set; }
}
