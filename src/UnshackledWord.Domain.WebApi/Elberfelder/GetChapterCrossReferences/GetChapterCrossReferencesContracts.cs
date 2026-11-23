using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

public sealed class GetChapterCrossReferencesRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }

    /// <summary>Query <c>includeHidden=true</c>: hidden links come back with <c>rating</c> <c>hide</c>. Honoured only for Admin/Maintainer, ignored for everyone else.</summary>
    public bool IncludeHidden { get; set; }
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
