namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class BibleStructureVersesDbo
{
    public const string DbName = "\"unshackled-word\".\"BibleStructureVerses\"";

    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int LastVerse { get; set; }
    public int? AltChapter { get; set; }
    public int? AltLastVerse { get; set; }
}
