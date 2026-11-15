namespace UnshackledWord.Domain.WebApi.Revision;

// Request/response of GET api/revision/overview (Bible revision, roadmap 0011.9). camelCase JSON, nulls omitted.

/// <summary>Names of the work lists of the overview (query parameter <c>list</c>).</summary>
public static class RevisionWorkListNames
{
    /// <summary>Variants whose mapping nobody saved yet (<c>MappingReviewedAt IS NULL</c>).</summary>
    public const string Unreviewed = "unreviewed";

    /// <summary>Variants with German words or original words without a link.</summary>
    public const string Incomplete = "incomplete";

    /// <summary>Variants whose <c>BaseTextHash</c> no longer matches the Elb1871 verse.</summary>
    public const string Stale = "stale";

    public static readonly string[] All = [Unreviewed, Incomplete, Stale];
}

public sealed class RevisionOverviewRequest
{
    /// <summary>One of <see cref="RevisionWorkListNames"/>; omitted = counts and level tiles only.</summary>
    public string? List { get; set; }

    /// <summary>Only variants of this level in the work list.</summary>
    public int? Level { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class RevisionOverviewResponse
{
    /// <summary>One entry per preset except the base level 100, sorted by level.</summary>
    public List<RevisionLevelOverview> Levels { get; set; } = [];
    public RevisionWorkCounts Counts { get; set; } = new();

    /// <summary>The requested page of one work list; absent when no <c>list</c> was asked for.</summary>
    public RevisionWorkList? List { get; set; }
}

public sealed class RevisionLevelOverview
{
    public int Level { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int RevisedVerses { get; set; }

    /// <summary>All Elb1871 verses; the denominator of the tile.</summary>
    public int TotalVerses { get; set; }
    public List<RevisionBookCount> Books { get; set; } = [];
}

public sealed class RevisionBookCount
{
    public int BibleBookId { get; set; }
    public int RevisedVerses { get; set; }
}

/// <summary>Sizes of the three work lists over all levels.</summary>
public sealed class RevisionWorkCounts
{
    public int Unreviewed { get; set; }
    public int Incomplete { get; set; }
    public int Stale { get; set; }
}

public sealed class RevisionWorkList
{
    public string Name { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<RevisionWorkItem> Items { get; set; } = [];
}

public sealed class RevisionWorkItem
{
    public int RevisionVerseId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UnlinkedGermanWords { get; set; }
    public int UnlinkedOriginalWords { get; set; }
}
