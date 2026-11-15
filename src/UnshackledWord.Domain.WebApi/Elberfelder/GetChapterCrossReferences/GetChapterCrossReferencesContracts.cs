using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

public sealed class GetChapterCrossReferencesRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
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

    /// <summary>TSK order, duplicates removed (first wins).</summary>
    public List<CrossReferenceResponse> References { get; set; } = [];
}
