namespace UnshackledWord.Domain.Models.Dbo;

public class Elb1871WordDbo
{
    public const string DboName = "\"unshackled-word\".\"Elb1871Words\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string WordInContext { get; set; } = default!;
    public string? PlainWord { get; set; }
}
