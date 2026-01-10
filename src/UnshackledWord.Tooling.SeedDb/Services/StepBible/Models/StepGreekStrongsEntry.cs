namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public sealed class StepGreekStrongsEntry : StepStrongsBaseEntry
{
    public string Greek
    {
        get => OriginalWord;
        set => OriginalWord = value;
    }
    public string AbbottSmithLexicon { get; set; } = default!;
}
