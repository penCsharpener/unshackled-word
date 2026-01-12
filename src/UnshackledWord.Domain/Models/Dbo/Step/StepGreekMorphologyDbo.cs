namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepGreekMorphologyDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepGreekMorphology\"";

    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string PartOfSpeech { get; set; } = default!;
    public string? Tense { get; set; }
    public string? Voice { get; set; }
    public string? Mood { get; set; }
    public string? Person { get; set; }
    public string? Number { get; set; }
    public string? Case { get; set; }
    public string? Gender { get; set; }
    public string? Degree { get; set; }
    public string? Extras { get; set; }
    public string? NameType { get; set; }
}
