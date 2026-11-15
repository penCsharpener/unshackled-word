namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

public sealed class GetVersesOfChapterRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }

    /// <summary>Literalness level 1-1000; 200 (default) = base level, 0 = Elb1871 only.</summary>
    public int Level { get; set; } = 200;
}
