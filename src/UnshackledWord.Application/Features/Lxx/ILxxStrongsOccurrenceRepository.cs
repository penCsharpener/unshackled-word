namespace UnshackledWord.Application.Features.Lxx;

/// <summary>Read access for the LXX occurrences of a Strong's number (roadmap 0028.9): tagged words of <c>LxxWordTags</c> with their verse.</summary>
public interface ILxxStrongsOccurrenceRepository
{
    /// <summary>Tags with one of <paramref name="keys"/> as Strong's number, split by status (one indexed query).</summary>
    Task<LxxStrongsCounts> CountAsync(IReadOnlyList<string> keys, CancellationToken token = default);

    /// <summary>One page of confirmed tags in LXX reading order (<c>SortOrder</c>, word position), with the whole verse and the Elb1871 verse text.</summary>
    Task<IReadOnlyList<LxxStrongsOccurrenceRow>> GetConfirmedPageAsync(IReadOnlyList<string> keys, int skip, int take, CancellationToken token = default);
}

public sealed record LxxStrongsCounts(int Confirmed, int Proposed);

public sealed class LxxStrongsOccurrenceRow
{
    public int LxxWordId { get; init; }
    public int LxxVerseId { get; init; }
    public int Position { get; init; }
    public int BibleBookId { get; init; }
    public string TextForm { get; init; } = "";
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public int Subverse { get; init; }

    /// <summary>Elb1871 verse the unit maps to, else the anchor verse of an addition (see <see cref="ElbIsAnchor"/>); null when neither exists.</summary>
    public int? ElbHebRefId { get; init; }

    public bool ElbIsAnchor { get; init; }
    public string? ElbText { get; init; }

    /// <summary>Words of the LXX verse in order (filled by the repository).</summary>
    public List<LxxStrongsVerseWord> Words { get; init; } = [];
}

public sealed record LxxStrongsVerseWord(int Position, string Greek);
