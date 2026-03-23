namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt;

public sealed class ByzTxtWord
{
    public const string DboName = "\"unshackled-word\".\"ByzTxtWords\"";

    public int LxxRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string Word { get; set; } = default!;
    public string StrongNumber { get; set; } = default!;
    public string Morphology { get; set; } = default!;
}
