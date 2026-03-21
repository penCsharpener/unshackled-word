namespace UnshackledWord.Domain.Models.Dbo;

public sealed class BibleVerseCountingMappingDbo
{
    private const string DboName = "\"unshackled-word\".\"BibleVerseCountingMapping\"";

    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int LxxRefId { get; set; }
}
