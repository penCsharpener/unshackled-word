namespace UnshackledWord.Domain.Models.Dbo;

public class Elb1871WordDbo
{
    public const string DboName = "\"unshackled-word\".\"Elb1871Words\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string WordInContext { get; set; } = default!;
    public string? PlainWord { get; set; }
    public string? Lemma { get; set; }
    public int PositionInVerse { get; set; }
    public string? Strongs { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? GrammaticalKey { get; set; }
}
