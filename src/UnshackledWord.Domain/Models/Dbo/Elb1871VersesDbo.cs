namespace UnshackledWord.Domain.Models.Dbo;

public class Elb1871VersesDbo
{
    public const string DboName = "\"unshackled-word\".\"Elb1871Verses\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public string VerseText { get; set; } = default!;
}
