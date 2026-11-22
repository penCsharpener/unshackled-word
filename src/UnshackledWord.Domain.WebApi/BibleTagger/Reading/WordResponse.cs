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

    /// <summary>Set for words of a revision variant (Id is then the base word or 0).</summary>
    public int? RevisionWordId { get; set; }

    /// <summary>Revision words only: the Elb1871 <c>PositionInVerse</c> the word is based on (null for a variant-only word). Note anchors use it (roadmap 0023.2f).</summary>
    public int? BasePositionInVerse { get; set; }
}
