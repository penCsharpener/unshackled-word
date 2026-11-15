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

/// <summary>The texts of all TSK targets of one verse, in TSK order (same order and de-duplication as the chapter cross references).</summary>
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

    /// <summary>The verse, or all verses of the range.</summary>
    public List<ChapterVerseResponse> Verses { get; set; } = [];
}
