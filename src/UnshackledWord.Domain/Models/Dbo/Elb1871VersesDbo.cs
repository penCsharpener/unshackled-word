namespace UnshackledWord.Domain.Models.Dbo;

public class Elb1871VersesDbo
{
    public const string DbName = "\"unshackled-word\".\"Elb1871Verses\"";

    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int LxxRefId { get; set; }
    public string VerseText { get; set; } = default!;
}
