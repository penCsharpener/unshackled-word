namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

public sealed class GetVersesOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<ChapterVerseResponse> Verses { get; set; } = [];
}

public sealed class ChapterVerseResponse
{
    public int Verse { get; set; }
    public string Text { get; set; } = default!;
}
