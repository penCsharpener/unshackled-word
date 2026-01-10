using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public sealed class StepGreekMorphologyEntry
{
    [Name("Code")]
    public string Code { get; set; } = default!;

    [Name("Function")]
    public string PartOfSpeech { get; set; } = default!;

    [Name("Tense")]
    public string Tense { get; set; } = default!;

    [Name("Voice")]
    public string Voice { get; set; } = default!;

    [Name("Mood")]
    public string Mood { get; set; } = default!;

    [Name("Person")]
    public string Person { get; set; } = default!;

    [Name("Number")]
    public string Number { get; set; } = default!;

    [Name("Case")]
    public string Case { get; set; } = default!;

    [Name("Gender")]
    public string Gender { get; set; } = default!;

    [Name("Degree")]
    public string Degree { get; set; } = default!;

    [Name("Extras")]
    public string Extras { get; set; } = default!;

    [Name("Name type")]
    public string NameType { get; set; } = default!;
}
