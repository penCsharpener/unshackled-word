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

    /// <summary>Original-language text of the verse; only set by the cross reference texts, omitted otherwise.</summary>
    public VerseOriginalTextResponse? Original { get; set; }
}

/// <summary>Plain surface words of the original-language verse (no morphology), joined with spaces.</summary>
public sealed class VerseOriginalTextResponse
{
    /// <summary>"hebrew", "aramaic" (verse mostly Aramaic, e.g. Daniel 2-7, Ezra 4-7) or "greek".</summary>
    public string Language { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
