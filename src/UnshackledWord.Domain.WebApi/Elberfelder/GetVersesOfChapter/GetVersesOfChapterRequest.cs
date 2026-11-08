namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

public sealed class GetVersesOfChapterRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
}
