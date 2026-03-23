namespace UnshackledWord.Domain.Models.Dbo;

public class GbtSourceWordDbo
{
    public const string DboName = "\"unshackled-word\".\"GbtSourceWords\"";

    public int LxxRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string SourceWord { get; set; } = default!;
    public string GrammarKey { get; set; } = default!;
}
