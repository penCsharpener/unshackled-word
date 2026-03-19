namespace UnshackledWord.Domain.Models.Dbo;

public sealed class SblTextDbo
{
    public const string DboName = "\"unshackled-word\".\"SblText\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public string VerseText { get; set; } = default!;
}