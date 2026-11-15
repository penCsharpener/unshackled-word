namespace UnshackledWord.Domain.Models.Study;

/// <summary>A verse as shown at a literalness level: the Elb1871 text, or the text of a revision variant (RevisionVerseId set).</summary>
public sealed class EffectiveElbVerse
{
    public int HebRefId { get; init; }
    public string VerseText { get; init; } = default!;
    public int? RevisionVerseId { get; init; }
    public int? RevisionLevel { get; init; }
}
