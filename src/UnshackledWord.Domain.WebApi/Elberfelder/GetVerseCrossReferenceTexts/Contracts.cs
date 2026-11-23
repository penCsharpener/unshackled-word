using UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVerseCrossReferenceTexts;

public sealed class GetVerseCrossReferenceTextsRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }

    /// <summary>Literalness level 1-1000; 200 (default) = base level, 0 = Elb1871 only.</summary>
    public int Level { get; set; } = 200;
}

/// <summary>The texts of all TSK targets of one verse, in the same order, de-duplication and hiding as the chapter cross references (highlighted first, then TSK order, added links last).</summary>
public sealed class GetVerseCrossReferenceTextsResponse
{
    public List<CrossReferenceTextResponse> References { get; set; } = [];
}

public sealed class CrossReferenceTextResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int? EndVerse { get; set; }

    /// <summary><c>highlight</c> or omitted (hidden links are not listed), roadmap 0024.3.</summary>
    public string? Rating { get; set; }

    /// <summary>Curator comment, omitted when there is none.</summary>
    public string? Comment { get; set; }

    /// <summary><c>added</c> for a link a curator added (roadmap 0024.4); omitted for TSK links.</summary>
    public string? Origin { get; set; }

    /// <summary>Word link in the source verse (roadmap 0024.5); omitted when the link is not attached to a word.</summary>
    public UnshackledWord.Domain.WebApi.Tsk.TskWordResponse? Word { get; set; }

    /// <summary>The verse, or all verses of the range.</summary>
    public List<ChapterVerseResponse> Verses { get; set; } = [];
}
