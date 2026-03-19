namespace UnshackledWord.Domain.Models.Dbo;

public sealed class TskDbo
{
    public const string DboName = "\"unshackled-word\".\"Tsk\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public string Scope { get; set; } = default!;
    public int RelatedStartBibleBookId { get; set; }
    public int RelatedStartChapter { get; set; }
    public int RelatedStartVerse { get; set; }
    public int RelatedStartRefId { get; set; }
    public int? RelatedEndBibleBookId { get; set; }
    public int? RelatedEndChapter { get; set; }
    public int? RelatedEndVerse { get; set; }
    public int? RelatedEndRefId { get; set; }
}