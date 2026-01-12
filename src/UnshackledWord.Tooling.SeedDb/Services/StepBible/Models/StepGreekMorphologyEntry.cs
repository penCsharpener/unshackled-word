using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public sealed class StepGreekMorphologyEntry
{
    [Name("Code")]
    public string Code { get; set; } = default!;

    [Name("Function")]
    public string PartOfSpeech { get; set; } = default!;

    [Name("Tense")]
    public string? Tense { get; set; }

    [Name("Voice")]
    public string? Voice { get; set; }

    [Name("Mood")]
    public string? Mood { get; set; }

    [Name("Person")]
    public string? Person { get; set; }

    [Name("Number")]
    public string? Number { get; set; }

    [Name("Case")]
    public string? Case { get; set; }

    [Name("Gender")]
    public string? Gender { get; set; }

    [Name("Degree")]
    public string? Degree { get; set; }

    [Name("Extras")]
    public string? Extras { get; set; }

    [Name("Name type")]
    public string? NameType { get; set; }
}
