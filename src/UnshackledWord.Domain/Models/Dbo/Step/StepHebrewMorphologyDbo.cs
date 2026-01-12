namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepHebrewMorphologyDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepHebrewMorphology\"";

    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string PartOfSpeech { get; set; } = default!;
    public string? Form { get; set; } = default!;
    public string? Tense { get; set; }
    public string? Mood { get; set; }
    public string? Person { get; set; }
    public string? Number { get; set; }
    public string? Gender { get; set; }
    public string? State { get; set; }
    public string? Stem { get; set; }
    public string? Action { get; set; }
    public string? Voice { get; set; }
}
