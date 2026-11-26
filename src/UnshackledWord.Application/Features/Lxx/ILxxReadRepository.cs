namespace UnshackledWord.Application.Features.Lxx;

/// <summary>Read access to <c>LxxVerses</c>/<c>LxxWords</c> for the reader (roadmap 0028.4).</summary>
public interface ILxxReadRepository
{
    /// <summary>
    /// Everything for the Elberfelder verses <paramref name="fromHebRefId"/>..<paramref name="toHebRefId"/>: the Elb1871 verse ids, the LXX units that map to
    /// them or are additions after them (in <c>SortOrder</c>), and the words of those units. One round trip.
    /// </summary>
    Task<LxxChapterRows> GetByHebRangeAsync(int fromHebRefId, int toHebRefId, CancellationToken token = default);
}

public sealed class LxxChapterRows
{
    public List<int> ElbHebRefIds { get; init; } = [];
    public List<LxxUnitRow> Units { get; init; } = [];
    public List<LxxWordRow> Words { get; init; } = [];
}

public sealed class LxxUnitRow
{
    public int Id { get; init; }
    public int BibleBookId { get; init; }
    public string TextForm { get; init; } = "";
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public int Subverse { get; init; }
    public int SortOrder { get; init; }
    public int? HebRefId { get; init; }
    public int? AfterHebRefId { get; init; }
    public string MapKind { get; init; } = "";
}

public sealed class LxxWordRow
{
    public int Id { get; init; }
    public int LxxVerseId { get; init; }
    public int Position { get; init; }
    public string Greek { get; init; } = "";
}
