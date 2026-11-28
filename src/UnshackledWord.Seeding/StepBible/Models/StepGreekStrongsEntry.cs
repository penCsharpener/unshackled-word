namespace UnshackledWord.Seeding.StepBible.Models;

public sealed class StepGreekStrongsEntry : StepStrongsBaseEntry
{
    public string Greek
    {
        get => OriginalWord;
        set => OriginalWord = value;
    }

    public string GreekNoDiacritics
    {
        get => OriginalWordNoDiacritics;
        set => OriginalWordNoDiacritics = value;
    }

    public string AbbottSmithLexicon { get; set; } = default!;
}
