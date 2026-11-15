namespace UnshackledWord.Domain.WebApi.Elberfelder;

/// <summary>A verse of a chapter read at a level other than 100 that shows a revision variant instead of the Elb1871 text.</summary>
public sealed class RevisedVerseResponse
{
    public int Verse { get; set; }
    public int Level { get; set; }
    public int RevisionVerseId { get; set; }
}
