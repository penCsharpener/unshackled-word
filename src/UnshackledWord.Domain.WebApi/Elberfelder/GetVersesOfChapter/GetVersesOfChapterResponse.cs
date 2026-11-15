namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

public sealed class GetVersesOfChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<ChapterVerseResponse> Verses { get; set; } = [];

    /// <summary>Verses showing a variant; null (omitted) when none, so level 100 is unchanged.</summary>
    public List<RevisedVerseResponse>? RevisedVerses { get; set; }
}

public sealed class ChapterVerseResponse
{
    public int Verse { get; set; }
    public string Text { get; set; } = default!;
}
