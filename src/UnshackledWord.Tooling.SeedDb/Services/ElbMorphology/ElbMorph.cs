using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class ElbMorph
{
    [Name("HebRefId")]
    public int HebRefId { get; set; }

    [Name("PositionInVerse")]
    public int PositionInVerse { get; set; }

    [Name("PlainWord")]
    public string PlainWord { get; set; } = default!;

    [Name("lemma")]
    public string Lemma { get; set; } = default!;

    [Name("part_of_speech")]
    public string PartOfSpeech { get; set; } = default!;

    [Name("degree")]
    public string? Degree { get; set; }

    [Name("nonfinite")]
    public string? VerbForm { get; set; }

    [Name("category")]
    public string Stts { get; set; } = default!;

    [Name("tense")]
    public string? Tense { get; set; }

    [Name("person")]
    public string? Person { get; set; }

    [Name("number")]
    public string? Number { get; set; }

    [Name("mood")]
    public string? Mood { get; set; }

    [Name("case")]
    public string? Case { get; set; }

    [Name("gender")]
    public string? Gender { get; set; }
}
