namespace UnshackledWord.Application.Features.Lxx;

/// <summary>Read access to <c>LxxVerses</c>/<c>LxxWords</c>/<c>LxxWordTags</c> for the reader (roadmap 0028.4, tags 0028.5).</summary>
public interface ILxxReadRepository
{
    /// <summary>
    /// Everything for the Elberfelder verses <paramref name="fromHebRefId"/>..<paramref name="toHebRefId"/>: the Elb1871 verse ids, the LXX units that map to
    /// them or are additions after them (in <c>SortOrder</c>), and the words of those units with their tag of any status (the caller filters by
    /// role, <see cref="LxxTagVisibility"/>). One round trip.
    /// </summary>
    Task<LxxChapterRows> GetByHebRangeAsync(int fromHebRefId, int toHebRefId, CancellationToken token = default);

    /// <summary>One word with its unit and its tag of any status; null when no LXX word has this id.</summary>
    Task<LxxWordDetailRow?> GetWordAsync(int lxxWordId, CancellationToken token = default);
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

public class LxxWordRow
{
    public int Id { get; init; }
    public int LxxVerseId { get; init; }
    public int Position { get; init; }
    public string Greek { get; init; } = "";

    /// <summary>Tag columns (<c>LxxWordTags</c>), all null for an untagged word.</summary>
    public string? Lemma { get; init; }

    public string? Strongs { get; init; }
    public string? Morphology { get; init; }

    /// <summary>proposed | confirmed; null = untagged.</summary>
    public string? Status { get; init; }

    public string? Source { get; init; }

    /// <summary>TBESG gloss of <see cref="Strongs"/> (exact disambiguation first, then the plain number).</summary>
    public string? Gloss { get; init; }

    /// <summary><c>LxxHebrewLinks</c> (0028.10): STEP Hebrew word ids of confirmed links; null/empty when none.</summary>
    public int[]? ConfirmedHebrewWordIds { get; init; }

    /// <summary>STEP Hebrew word ids of proposed links (only Admin/Maintainer get them returned).</summary>
    public int[]? ProposedHebrewWordIds { get; init; }
}

/// <summary>A word with the unit it belongs to (word box).</summary>
public sealed class LxxWordDetailRow : LxxWordRow
{
    public int BibleBookId { get; init; }
    public string TextForm { get; init; } = "";
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public int Subverse { get; init; }
    public int? HebRefId { get; init; }
    public int? AfterHebRefId { get; init; }
    public string MapKind { get; init; } = "";
}
