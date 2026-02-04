namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt;

public sealed class ByzTxtWord
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int SortNumber { get; set; }
    public string Word { get; set; } = default;
    public string StrongNumber { get; set; } = default;
    public string Morphology { get; set; } = default;
}