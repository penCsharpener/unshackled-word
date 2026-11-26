namespace UnshackledWord.Domain.WebApi.Lxx;

/// <summary><c>GET api/lxx/chapter/{bibleBookId}/{chapter}</c> (roadmap 0028.4): Elberfelder (Hebrew) counting, Old Testament books 1..39 only.</summary>
public sealed class GetLxxChapterRequest
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
}

/// <summary>
/// The Septuagint text of one Elberfelder chapter: one entry per Elb1871 verse, with an empty <c>units</c> list when the LXX has no text for it.
/// Daniel carries both text forms (<c>og</c> and <c>th</c>); the client filters.
/// </summary>
public sealed class GetLxxChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<LxxElbVerseResponse> Verses { get; set; } = [];
}

/// <summary>The LXX units of one Elb1871 verse in reading order (Swete's order): mapped verses (merged/split can give several) and additions that belong to this verse.</summary>
public sealed class LxxElbVerseResponse
{
    public int HebRefId { get; set; }
    public int Verse { get; set; }
    public List<LxxUnitResponse> Units { get; set; } = [];
}

/// <summary>One Swete unit with its Rahlfs label. Extension point for 0028.5: tags per word are added to <see cref="LxxWordResponse"/>.</summary>
public sealed class LxxUnitResponse
{
    public int LxxVerseId { get; set; }

    /// <summary>Rahlfs counting.</summary>
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>0 for a whole verse; k for the k-th marker segment (<c>1Ki.2:35.1</c>).</summary>
    public int Subverse { get; set; }

    /// <summary>Rahlfs reference as <c>Jer.32:1</c> or <c>1Ki.2:35.1</c> (TVTMS book code); the client formats it.</summary>
    public string Ref { get; set; } = "";

    /// <summary>"" | "og" | "th".</summary>
    public string TextForm { get; set; } = "";

    /// <summary>one | merged | split | addition.</summary>
    public string MapKind { get; set; } = "";

    public bool IsAddition { get; set; }

    /// <summary>True when the Rahlfs label differs from the Elb verse (other chapter or verse number, a subverse, or an addition): the client shows the label.</summary>
    public bool DiffersFromHebrew { get; set; }

    public List<LxxWordResponse> Words { get; set; } = [];
}

public sealed class LxxWordResponse
{
    public int Id { get; set; }
    public int Position { get; set; }
    public string Greek { get; set; } = "";
}
