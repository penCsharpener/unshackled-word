namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class BibleStructureChaptersDbo
{
    public const string DbName = "\"unshackled-word\".\"BibleStructureChapters\"";

    public int BibleBookId { get; set; }
    public int LastChapter { get; set; }
    public int? AltLastChapter { get; set; }
}
