namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public sealed class StepHebrewStrongsEntry : StepStrongsBaseEntry
{
    public string Hebrew
    {
        get => OriginalWord;
        set => OriginalWord = value;
    }

    public string HebrewNoDiacritics
    {
        get => OriginalWordNoDiacritics;
        set => OriginalWordNoDiacritics = value;
    }

    public string Meaning { get; set; } = default!;
}
