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
    public string Tense { get; set; } = default!;

    [Name("Form:Mood")]
    public string Mood { get; set; } = default!;

    [Name("Person")]
    public string Person { get; set; } = default!;

    [Name("Number")]
    public string Number { get; set; } = default!;

    [Name("Gender")]
    public string Gender { get; set; } = default!;

    [Name("State")]
    public string State { get; set; } = default!;

    [Name("Stem")]
    public string Stem { get; set; } = default!;

    [Name("Stem:Action")]
    public string Action { get; set; } = default!;

    [Name("Stem:Voice")]
    public string Voice { get; set; } = default!;
}
