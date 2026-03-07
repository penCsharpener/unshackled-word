namespace UnshackledWord.Domain.WebApi.BibleTagger.Reading;

public record WordResponse
{
    public int Verse { get; set; }
    public int PositionInVerse { get; set; }
    public string WordInContext { get; set; } = default!;
    public string? PlainWord { get; set; }
    public string? Lemma { get; set; }
    public string? Original { get; set; }
    public string? OriginalLemma { get; set; }
    public string? Strongs { get; set; }
    public int Id { get; set; }
    public string VerseCode { get; set; } = default!;
    public string? GrammaticalKey { get; set; }
}
