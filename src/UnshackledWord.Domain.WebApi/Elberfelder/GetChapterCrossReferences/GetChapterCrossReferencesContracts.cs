using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

public sealed class GetChapterCrossReferencesRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }

    /// <summary>Query <c>includeHidden=true</c>: hidden links come back with <c>rating</c> <c>hide</c>. Honoured only for Admin/Maintainer, ignored for everyone else.</summary>
    public bool IncludeHidden { get; set; }

    /// <summary>
    /// Query <c>level</c> (0–1000, roadmap 0024.5): 0 or omitted = Elb1871; otherwise word links on verses with a revision variant at that level carry
    /// <c>word.mappedWords</c> (same variant selection as the reader and the notes).
    /// </summary>
    public int? Level { get; set; }
}

/// <summary>TSK cross references of a whole chapter, one entry per verse that has any. Verses without references are omitted.</summary>
public sealed class GetChapterCrossReferencesResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<VerseCrossReferencesResponse> Verses { get; set; } = [];
}

public sealed class VerseCrossReferencesResponse
{
    public int Verse { get; set; }

    /// <summary>Highlighted links first, then the rest; within each group TSK order with added links (<c>origin</c> <c>added</c>) last in creation order. Duplicates removed (first wins), hidden links dropped.</summary>
    public List<CrossReferenceResponse> References { get; set; } = [];
}
