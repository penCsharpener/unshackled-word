using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Domain.WebApi.Revision;

// Request/response bodies of /api/revision/* (Bible revision, roadmap 0011). camelCase JSON, nulls omitted.

/// <summary>One slider stop; <c>GET api/revision/levels</c> returns a bare array of these, sorted by level.</summary>
public sealed class RevisionLevelEntry
{
    public int Level { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>Name in the request language (Accept-Language: <c>de</c> or else English).</summary>
    public string Name { get; set; } = string.Empty;
    public bool IsBase { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>Route of one Elb1871 verse (book 1-66, chapter, verse); shared by the verse endpoints.</summary>
public class RevisionVerseRouteRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
}

/// <summary>Body of <c>POST api/revision/verse/bookId/{b}/chapterId/{c}/verseId/{v}</c>: a new version of the variant at <see cref="Level"/>.</summary>
public sealed class SaveRevisionVerseRequest : RevisionVerseRouteRequest
{
    public int Level { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>Active version the editor was loaded with; omitted when the level has no variant yet. Anything else answers 409.</summary>
    public int? ExpectedVersion { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// The text the new version starts from for the mapping carry-over: 100 = Elb1871 (default), otherwise the active variant at that
    /// level, whose mapping (manual links included) is carried instead of Elb1871's. The client sends the text itself.
    /// </summary>
    public int BasedOnLevel { get; set; } = 0;
}

public sealed class SaveRevisionVerseResponse
{
    public int RevisionVerseId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }

    /// <summary>What the carry-over of the mapping from the parent text did (roadmap 0011.4).</summary>
    public RevisionCarryReportEntry Carry { get; set; } = new();
}

/// <summary>Counts of the revision's German words by relation to the parent text, plus the original words that lost their link.</summary>
public sealed class RevisionCarryReportEntry
{
    /// <summary>Words identical to the parent word, mapping copied.</summary>
    public int Same { get; set; }

    /// <summary>Words that replace a parent word; mapping copied but to be checked.</summary>
    public int Changed { get; set; }

    /// <summary>Words without a parent word; no mapping.</summary>
    public int New { get; set; }

    /// <summary>STEP word ids that were linked in the parent text and have no link in the new text.</summary>
    public List<int> LostOriginalWordIds { get; set; } = [];
}

/// <summary>Response of <c>GET api/revision/verse/…</c>: the Elb1871 text and every active variant of the verse.</summary>
public sealed class RevisionVerseResponse
{
    public string BaseText { get; set; } = string.Empty;
    public List<RevisionVariantEntry> Variants { get; set; } = [];
}

public sealed class RevisionVariantEntry
{
    public int RevisionVerseId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Display name of the author; absent for variants imported from the repo seed.</summary>
    public string? CreatedBy { get; set; }
    public DateTime? MappingReviewedAt { get; set; }
}

/// <summary>Route of <c>GET/PUT api/revision/verseMapping/{revisionVerseId}</c>.</summary>
public class RevisionVerseMappingRouteRequest
{
    public int RevisionVerseId { get; set; }
}

/// <summary>
/// Mapping of one variant in the shape of <c>GetVerseMappingResponse</c>. <c>germanWords[].id</c> and <c>links[].elbWordId</c> /
/// <c>parentElbWordId</c> hold <c>RevisionWords.Id</c> (the field names stay so the mapping editor works unchanged).
/// </summary>
public sealed class RevisionVerseMappingResponse
{
    public int RevisionVerseId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }

    /// <summary>False when a newer version replaced this one or the variant was withdrawn.</summary>
    public bool IsActive { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>"greek" (New Testament) or "hebrew" (Old Testament).</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Set once a writer saved the mapping of this version; NULL = still what the carry-over produced.</summary>
    public DateTime? MappingReviewedAt { get; set; }
    public List<RevisionMappingGermanWord> GermanWords { get; set; } = [];
    public List<VerseMappingOriginalWord> OriginalWords { get; set; } = [];
    public List<RevisionMappingLink> Links { get; set; } = [];
}

public sealed class RevisionMappingGermanWord
{
    public int Id { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>same, changed or new (relative to the parent text).</summary>
    public string Change { get; set; } = string.Empty;
}

public sealed class RevisionMappingLink
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }

    /// <summary>carried, manual or seed.</summary>
    public string Origin { get; set; } = string.Empty;
}

/// <summary>Body of <c>PUT api/revision/verseMapping/{revisionVerseId}</c>: the complete new mapping of the variant.</summary>
public sealed class SaveRevisionMappingRequest : RevisionVerseMappingRouteRequest
{
    public List<VerseMappingLink> Links { get; set; } = [];
}

/// <summary>Query of <c>GET api/revision/verse/…/history?level=</c>.</summary>
public sealed class RevisionHistoryRequest : RevisionVerseRouteRequest
{
    public int Level { get; set; }
}

/// <summary>All versions of one level of a verse, newest first.</summary>
public sealed class RevisionHistoryResponse
{
    public int Level { get; set; }
    public List<RevisionHistoryEntry> Versions { get; set; } = [];
}

public sealed class RevisionHistoryEntry
{
    public int RevisionVerseId { get; set; }
    public int Version { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Display name of the author; absent for variants imported from the repo seed.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>When a newer version replaced this one or the variant was withdrawn; absent for the active version.</summary>
    public DateTime? EndedAt { get; set; }
}

/// <summary>Route of <c>DELETE api/revision/verse/{revisionVerseId}</c>.</summary>
public sealed class WithdrawRevisionRequest
{
    public int RevisionVerseId { get; set; }
}

/// <summary>A literalness preset with both names, for the maintainer's level management (<c>GET api/revision/levels/manage</c>).</summary>
public sealed class RevisionLevelDetail
{
    public int Level { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameDe { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsBase { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>Number of stored variant versions at this level; the level value is fixed once this is above 0.</summary>
    public int VariantCount { get; set; }
}

/// <summary>Body of <c>POST api/revision/levels</c> and <c>PUT api/revision/levels/{currentLevel}</c> (there <see cref="Level"/> is the new value).</summary>
public sealed class SaveRevisionLevelRequest
{
    /// <summary>Only set on PUT: the level in the route.</summary>
    public int CurrentLevel { get; set; }
    public int Level { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameDe { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsDefault { get; set; }
}
