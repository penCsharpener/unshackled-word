using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt;

public sealed class ByzTxtEntity
{
    [Ignore]
    public int BibleBookId { get; set; }
    [Name("chapter")]
    public int Chapter { get; set; }
    [Name("verse")]
    public int Verse { get; set; }
    [Name("text")]
    public string CsvText { get; set; } = default;
    [Ignore]
    public List<ByzTxtWord> ByzWords { get; set; } = new();
}