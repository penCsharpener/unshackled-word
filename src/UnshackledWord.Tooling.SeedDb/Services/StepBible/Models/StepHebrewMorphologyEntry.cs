using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public sealed class StepHebrewMorphologyEntry
{
    [Name("Code")]
    public string Code { get; set; } = default!;

    [Name("Function")]
    public string PartOfSpeech { get; set; } = default!;

    [Name("Form")]
    public string Form { get; set; } = default!;

    [Name("Form:Tense")]
    public string? Tense { get; set; }

    [Name("Form:Mood")]
    public string? Mood { get; set; }

    [Name("Person")]
    public string? Person { get; set; }

    [Name("Number")]
    public string? Number { get; set; }

    [Name("Gender")]
    public string? Gender { get; set; }

    [Name("State")]
    public string? State { get; set; }

    [Name("Stem")]
    public string? Stem { get; set; }

    [Name("Stem:Action")]
    public string? Action { get; set; }

    [Name("Stem:Voice")]
    public string? Voice { get; set; }
}
