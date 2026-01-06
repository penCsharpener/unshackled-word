namespace UnshackledWord.Domain.Models.Dbo;

public sealed class Elb1871SrGntTaggingDbo
{
    public const string DboName = "\"unshackled-word\".\"Elb1871SrGntTagging\"";
    public int Id { get; set; }
    public int Elb1871WordsId { get; set; }
    public int SrGntWordsId { get; set; }
    public int PositionInVerse { get; set; }
    public string Comment { get; set; } = default!;
}
